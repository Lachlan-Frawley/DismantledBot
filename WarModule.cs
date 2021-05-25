using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System;

namespace DismantledBot
{
    [Group("war")]
    public class WarModule : ModuleBase<SocketCommandContext>
    {
        public static ModuleSettingsManager<WarModule> settings = ModuleSettingsManager<WarModule>.MakeSettings();

        public static List<string> ExtractNamesFromEmbed(string input)
        {
            input = input.Substring(4);
            string[] splits = input.Split('\n');

            return splits.Select(x => x.Replace("\\", "")).ToList();
        }

        public static List<string> ExtractWarChannelNames(SocketCommandContext context)
        {
            List<SocketVoiceChannel> warChannels = context.Guild.GetCategoryChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARCAT_KEY)).Channels.Where(x => x is SocketVoiceChannel).Select(x => x as SocketVoiceChannel).ToList();
            Console.WriteLine($"War Channels:\n{string.Join(",\n", warChannels.Select(x => x.Name))}\n");

            List<SocketGuildUser> warUsers = warChannels.SelectMany(x => x.Users).ToList();
            return warUsers.Select(x => string.IsNullOrEmpty(x.Nickname) ? x.Username : x.Nickname).ToList();
        }

        public static async Task<List<string>> GetAllGuildMembers(SocketCommandContext context)
        {
            SocketRole selectedRole = context.Guild.GetRole(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_GMEMBER_KEY));
            Console.WriteLine($"Role Member Count: {selectedRole.Members.Count()}");
            Console.WriteLine($"Basic User Count = {context.Guild.Users.Count}");
            List<IGuildUser> users = (await context.Guild.GetUsersAsync().FlattenAsync()).ToList();
            Console.WriteLine($"Downloaded Users Count = {users.Count}");
            return context.Guild.Users.Where(x => x.Roles.Contains(selectedRole)).Select(x => string.IsNullOrEmpty(x.Nickname) ? x.Username : x.Nickname).ToList();
        }

        [Group("debug")]
        [IsCreator]
        public class DebugModule : ModuleBase<SocketCommandContext>
        {
            [Command("trial0", RunMode = RunMode.Async)]
            public async Task RunTrial(string day)
            {
                Regex nwRegex = new Regex("(n|N)(o|O)(d|D)(e|E) (w|W)(a|A)(r|R)");
                SocketTextChannel eventChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
                List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
                IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).Where(x => x.Embeds.First().Description.Contains(day, StringComparison.InvariantCultureIgnoreCase)).First();
                IEmbed targetEmbed = selectedMessage.Embeds.First();
                EmbedField targetField = targetEmbed.Fields[2];

                List<string> signupNames = ExtractNamesFromEmbed(targetField.Value);
                List<string> namesInChannels = ExtractWarChannelNames(Context);

                List<string> commonNames = signupNames.Intersect(namesInChannels).ToList();
                List<string> attendedButNotSignedUp = namesInChannels.Except(signupNames).ToList();

                //List<string> missingName = signupNames.Except(namesInChannels).ToList();

                List<string> allGuildMembers = await GetAllGuildMembers(Context);

                List<string> attended = new List<string>();
                List<string> missingName = new List<string>();

                Console.WriteLine("Reading Attending Names...");
                foreach(string name in commonNames.Union(attendedButNotSignedUp))
                {
                    if (attended.Contains(name))
                        continue;

                    if(allGuildMembers.Contains(name))
                    {
                        attended.Add(name);
                    } else
                    {
                        List<string> fuzzy = Utilities.FuzzySearch(allGuildMembers, name);
                        Console.WriteLine($"Couldn't find name [{name}], substituting with [{fuzzy.First()}]");
                        attended.Add(fuzzy.First());
                    }
                }

                Console.WriteLine("Reading Missing Names...");
                foreach(string name in signupNames.Except(namesInChannels))
                {
                    if (missingName.Contains(name))
                        continue;

                    if(allGuildMembers.Contains(name))
                    {
                        missingName.Add(name);
                    } else
                    {
                        List<string> fuzzy = Utilities.FuzzySearch(allGuildMembers, name);
                        Console.WriteLine($"Couldn't find name [{name}], substituting with [{fuzzy.First()}]");
                        missingName.Add(fuzzy.First());
                    }
                }

                EmbedBuilder builder = new EmbedBuilder();
                builder.Description = "TRIAL WAR ATTENDANCE";
                builder.AddField("Attendance", string.Join(",\n", attended));
                builder.AddField("Missing", string.Join(",\n", missingName));

                await ReplyAsync(message: $"Using signup from [{day}]", embed: builder.Build());
            }

            [Command("test_extract")]
            public async Task TestExtract()
            {
                Regex nwRegex = new Regex("(n|N)(o|O)(d|D)(e|E) (w|W)(a|A)(r|R)");
                SocketTextChannel eventChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
                List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
                IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).GetRandom();
                IEmbed targetEmbed = selectedMessage.Embeds.First();
                EmbedField targetField = targetEmbed.Fields[2];

                Console.WriteLine("********************************");
                Console.WriteLine($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
                Console.WriteLine($"Description: {targetEmbed.Description}");
                Console.WriteLine($"Target Name: {targetField.Name}");
                Console.WriteLine($"Target Data:\n{targetField.Value}");
                Console.WriteLine();
                Console.WriteLine("Found Names:");
                List<string> names = ExtractNamesFromEmbed(targetField.Value);
                Console.WriteLine(string.Join(",\n", names));
                Console.WriteLine();

                await ReplyAsync($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
            }

            [Command("field_extract")]
            public async Task FieldExtract(int messageNumber)
            {
                SocketTextChannel eventChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
                List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();

                IMessage extracted = messageNumber < 0 ? eventMessages[0] : messageNumber >= eventMessages.Count ? eventMessages[0] : eventMessages[messageNumber];

                Console.WriteLine("********************************");
                Console.WriteLine($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
                Console.WriteLine($"Embed Count: {extracted.Embeds.Count}\n");
                foreach (IEmbed embeds in extracted.Embeds)
                {
                    Console.WriteLine($"Description: {embeds.Description}\nField Counts: {embeds.Fields.Length}");
                    foreach(EmbedField field in embeds.Fields)
                    {
                        Console.WriteLine($"Field Name: {field.Name}, Value: {field.Value}");
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();

                await ReplyAsync($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
            }

            [Command("event_scan")]
            public async Task EventScan()
            {
                Regex nwRegex = new Regex("(n|N)(o|O)(d|D)(e|E) (w|W)(a|A)(r|R)");
                SocketTextChannel eventChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
                List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();

                Console.WriteLine("********************************");
                Console.WriteLine($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
                foreach (IMessage message in eventMessages)
                {
                    foreach (IEmbed embeded in message.Embeds)
                    {
                        Console.WriteLine(embeded.Description);
                        MatchCollection matches = nwRegex.Matches(embeded.Description);
                        Console.WriteLine($"Found {matches.Count} matches!");                        
                    }
                }
                Console.WriteLine();

                await ReplyAsync($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
            }
        }
    }
}
