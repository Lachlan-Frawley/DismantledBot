using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;

namespace DismantledBot
{
    [Group("bind")]
    public class BindingModule : ModuleBase<SocketCommandContext>
    {
        public const string BINDING_OWNER_KEY = "server_owner";
        public const string BINDING_SIGNUP_KEY = "event_signup";
        public const string BINDING_WARCAT_KEY = "war_category";
        public const string BINDING_WARDISC_KEY = "wdisk_key";
        public const string BINDING_GMEMBER_KEY = "guildmem_key";

        public static ModuleSettingsManager<BindingModule> settings = ModuleSettingsManager<BindingModule>.MakeSettings();

        [Command("debug")]
        [Summary("Prints debug information to the console")]
        [IsCreator]
        public async Task Debug()
        {
            Console.WriteLine("********************************");
            Console.WriteLine($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
            foreach(KeyValuePair<string, string> value in settings.GetAllDataAsString())
            {
                Console.WriteLine($"{value.Key} -> {value.Value}");
            }
            Console.WriteLine();

            await ReplyAsync($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
        }

        [Command("owner")]
        [Summary("Binds the bot to the server owner")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindOwner(ulong id)
        {
            settings.SetData(BINDING_OWNER_KEY, id);
            await ReplyAsync("Owner bind successful!");
        }

        [Command("signup")]
        [Summary("Binds the bot to the event signup channel")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindSignupChannel(ulong id)
        {
            settings.SetData(BINDING_SIGNUP_KEY, id);
            await ReplyAsync("Event signup bind successful!");
        }

        [Command("warcat")]
        [Summary("Binds the bot to the war channel category")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindWarCategory(ulong id)
        {
            settings.SetData(BINDING_WARCAT_KEY, id);
            await ReplyAsync("War categories bound successfully!");
        }

        [Command("wardisc")]
        [Summary("Binds the bot to the war discussion channel")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindWarDiscussion(ulong id)
        {
            settings.SetData(BINDING_WARDISC_KEY, id);
            await ReplyAsync("War discussion bound successfully!");
        }

        [Command("member_role")]
        [Summary("Binds the bot to the guild member role")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindMemberRole(ulong id)
        {
            settings.SetData(BINDING_GMEMBER_KEY, id);
            await ReplyAsync("Guild Member Role bound successfully!");
        }
    }
}
