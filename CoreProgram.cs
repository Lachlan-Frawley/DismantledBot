using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace DismantledBot
{
    public class CoreProgram
    {        
        public static void Main(string[] args)
            => new CoreProgram().MainAsync().GetAwaiter().GetResult();

        public static DiscordSocketClient client { get; private set; }
        public static BotControl settings { get; private set; } = BotControl.Load("config.json");
        public static Logger logger { get; private set; } = new Logger(settings.LogPath, settings.LogLevel);
        public static DatabaseManager database { get; private set; }
        public static SocketGuild BoundGuild { get; private set; }

        public async Task MainAsync()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            database = new DatabaseManager();

            client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true,
                GatewayIntents = 
                GatewayIntents.GuildVoiceStates |
                GatewayIntents.GuildMembers | 
                GatewayIntents.GuildMessages | 
                GatewayIntents.Guilds | 
                GatewayIntents.DirectMessages |
                GatewayIntents.DirectMessageReactions |
                GatewayIntents.GuildMessageReactions,
                LargeThreshold = 250
            });
            client.Log += Log;

            await client.LoginAsync(TokenType.Bot, settings.Token);
            await client.StartAsync();       

            CommandHandler handler = new CommandHandler(client, new CommandService());
            await handler.InstallCommandsAsync();

            client.Ready += async () =>
            {
                Console.WriteLine("Bot running....");
                try
                {
                    BoundGuild = client.Guilds.First();
                }
                catch
                {
                    Console.WriteLine("Failed to determine bound guild!");
                    Environment.Exit(-1);
                }
                var users = await BoundGuild.GetUsersAsync().FlattenAsync();
                try
                {
                    await database.ManageGuildMembers(new List<IGuildUser>(users));
                } catch(Exception e)
                {
                    Utilities.PrintException(e);
                    Environment.Exit(-1);
                }
            };

            await Task.Delay(-1);
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }
    }

    public class CommandHandler
    {
        public static List<ModuleInfo> Modules { get; private set; }

        private readonly DiscordSocketClient client;
        private readonly CommandService commands;

        public CommandHandler(DiscordSocketClient client, CommandService service)
        {
            this.client = client;
            commands = service;
        }

        public async Task InstallCommandsAsync()
        {
            client.MessageReceived += Client_MessageReceived;
            Modules = (await commands.AddModulesAsync(Assembly.GetEntryAssembly(), null)).ToList();

            client.GuildMemberUpdated += GuildMemberChanged;
            client.UserJoined += (user) =>
            {
                return GuildMemberChanged(null, user);
            };
            client.UserLeft += (user) =>
            {
                return GuildMemberChanged(user, null);
            };
            client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            client.ReactionAdded += Client_ReactionAdded;
            client.MessageDeleted += Client_MessageDeleted;            
        }

        private Task Client_MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            if(channel.Id == SettingsModule.settings.GetData<ulong>(SettingsModule.EVENT_SIGNUP_KEY))
            {
                WarUtility.HandleDeletedEventMessage(message, channel);
            }

            return Task.CompletedTask;
        }

        private Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if(channel.Id == SettingsModule.settings.GetData<ulong>(SettingsModule.EVENT_SIGNUP_KEY))
            {
                WarUtility.HandleSignupReact(message, channel, reaction);
            }

            return Task.CompletedTask;
        }

        private Task Client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState previous, SocketVoiceState current)
        {
            if(current.VoiceChannel != null && current.VoiceChannel.CategoryId.HasValue && current.VoiceChannel.CategoryId.Value == SettingsModule.settings.GetData<ulong>(SettingsModule.WAR_CATEGORY_KEY))
            {
                WarUtility.AddUserIntoAttendance(user.Id);
            }

            return Task.CompletedTask;
        }

        private Task GuildMemberChanged(SocketGuildUser before, SocketGuildUser after)
        {
            if (before != null && after != null)
            {
                try
                {
                    var preRoles = before.Roles.Select(x => x.Id);
                    var postRoles = after.Roles.Select(x => x.Id);
                    var teamRoles = CoreProgram.database.GetRows(new GuildTeams.Comparer()).Select(x => (ulong)x.TeamRole);
                    var preTeamRoles = preRoles.Where(x => teamRoles.Contains(x));
                    var postTeamRoles = postRoles.Where(x => teamRoles.Contains(x));

                    var removedTeamRoles = preTeamRoles.Except(postTeamRoles);
                    var addedTeamRoles = postTeamRoles.Except(preTeamRoles);

                    if (removedTeamRoles.Count() + addedTeamRoles.Count() != 0)
                    {
                        foreach (ulong removedRole in removedTeamRoles)
                        {
                            CoreProgram.database.DeleteSingle(new TeamMember(removedRole, after.Id), "TeamID", "DiscordID");
                        }
                        foreach (ulong addedRole in addedTeamRoles)
                        {
                            CoreProgram.database.InsertSingle(new TeamMember(addedRole, after.Id));
                        }
                    }

                    CoreProgram.logger.Write2(Logger.DEBUG, $"Modified {after}, added {addedTeamRoles.Count()} team roles, removed {removedTeamRoles.Count()} team roles...");
                } catch(Exception e)
                {
                    CoreProgram.logger.Write2(Logger.WARNING, e.Message);
                }

                if (before.Id == after.Id && string.Equals(before.Username, after.Username) && string.Equals(before.Nickname, after.Nickname))
                    return Task.CompletedTask;
                CoreProgram.logger.Write2(Logger.DEBUG, $"User Updated: {after}");
                CoreProgram.database.UpdateUser(after.FromIGuildUser());
            } else if (before != null && after == null)
            {
                CoreProgram.logger.Write2(Logger.DEBUG, $"User Removed: {before}");
                CoreProgram.database.RemoveUser(before.FromIGuildUser());
            } else if (before == null && after != null)
            {
                CoreProgram.logger.Write2(Logger.DEBUG, $"User Added: {after}");
                CoreProgram.database.AddUser(after.FromIGuildUser());
            } else
            {
                CoreProgram.logger.Write(Logger.ERROR, "Unknown state?");
            }

            return Task.CompletedTask;
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if(arg.Channel != null && arg.Channel.Id == SettingsModule.settings.GetData<ulong>(SettingsModule.EVENT_SIGNUP_KEY))
            {
                WarUtility.HandleNewEventMessage(arg);
                return;
            }

            var message = arg as SocketUserMessage;
            if (message == null)
                return;

            int argPos = 0;

            if (!(message.HasCharPrefix(CoreProgram.settings.Prefix, ref argPos)) || message.HasMentionPrefix(client.CurrentUser, ref argPos) || message.Author.IsBot)
                return;

            var context = new SocketCommandContext(client, message);            

            await commands.ExecuteAsync(context, argPos, null);
        }
    }
}
