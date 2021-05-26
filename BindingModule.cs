using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;

namespace DismantledBot
{
    // Binding settings module
    [Group("bind")]
    public class BindingModule : ModuleBase<SocketCommandContext>
    {
        // Settings keys
        public const string BINDING_OWNER_KEY = "server_owner";
        public const string BINDING_SIGNUP_KEY = "event_signup";
        public const string BINDING_WARCAT_KEY = "war_category";
        public const string BINDING_WARDISC_KEY = "wdisk_key";
        public const string BINDING_GMEMBER_KEY = "guildmem_key";

        public static ModuleSettingsManager<BindingModule> settings = ModuleSettingsManager<BindingModule>.MakeSettings();

        // Prints out saved setting info to console
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

        // Binds the server owner
        [Command("owner")]
        [Summary("Binds the bot to the server owner")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindOwner(ulong id)
        {
            settings.SetData(BINDING_OWNER_KEY, id);
            await ReplyAsync("Owner bind successful!");
        }

        // Binds the event signup channel
        [Command("signup")]
        [Summary("Binds the bot to the event signup channel")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindSignupChannel(ulong id)
        {
            settings.SetData(BINDING_SIGNUP_KEY, id);
            await ReplyAsync("Event signup bind successful!");
        }

        // Binds the node war category
        [Command("warcat")]
        [Summary("Binds the bot to the war channel category")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindWarCategory(ulong id)
        {
            settings.SetData(BINDING_WARCAT_KEY, id);
            await ReplyAsync("War categories bound successfully!");
        }

        // Binds the war discussion channel
        [Command("wardisc")]
        [Summary("Binds the bot to the war discussion channel")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task BindWarDiscussion(ulong id)
        {
            settings.SetData(BINDING_WARDISC_KEY, id);
            await ReplyAsync("War discussion bound successfully!");
        }

        // Binds the guild member role
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
