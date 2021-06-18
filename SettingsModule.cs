using Discord.Commands;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace DismantledBot
{
    public sealed class SettingsModule : ModuleBase<SocketCommandContext>
    {
        public static readonly ModuleSettingsManager<SettingsModule> settings = ModuleSettingsManager<SettingsModule>.MakeSettings();

        public const string WAR_CATEGORY_KEY = "WAR_CATEGORY_KEY";
        public const string WAR_DISCUSSN_KEY = "WAR_DISCUSSN_KEY";
        public const string EVENT_SIGNUP_KEY = "EVENT_SIGNUP_KEY";
        public const string SIGNUP_EMOTE_KEY = "SIGNUP_EMOTE_KEY";
        public const string MAIN_SGEMOTE_KEY = "MAIN_SGEMOTE_KEY";

        [Group("set")]
        [IsUser(Functions.Names.GET_OWNER_FUNC, Functions.Names.GET_ADMIN_FUNC)]
        public sealed class PrivilegedL2Commands : ModuleBase<SocketCommandContext>
        {
            [Command("owner")]
            [Summary("Sets the bound servers owner")]
            public async Task BindOwner([Summary("The ID of the owner")] ulong id)
            {
                settings.SetData(Functions.Names.GET_OWNER_FUNC, id);
                await ReplyAsync("Owner bound!");
            }

            [Command("member role")]
            [Summary("Sets the bound servers guild member role")]
            public async Task BindMemberRole([Summary("The ID of the guild member role")]ulong id)
            {
                settings.SetData(Functions.Names.GET_GUILD_MEMBER_FUNC, id);
                await ReplyAsync("Guild member role bound!");
            }

            [Command("officer role")]
            public async Task BindOfficerRole(ulong id)
            {
                settings.SetData(Functions.Names.GET_OFFICER_FUNC, id);
                await ReplyAsync("Officer role bound!");
            }

            [Command("war category")]
            [Summary("Sets the category for war voice channels")]
            public async Task BindWarCategory([Summary("The ID of the war category")]ulong id)
            {
                settings.SetData(WAR_CATEGORY_KEY, id);
                await ReplyAsync("Bound war category!");
            }

            [Command("war discussion")]
            public async Task BindWarDiscussion(ulong id)
            {
                settings.SetData(WAR_DISCUSSN_KEY, id);
                await ReplyAsync("Bound war discussion!");
            }

            [Command("event signup")]
            [Summary("Sets the event signup channel")]
            public async Task BindEventChannel([Summary("The ID of the event signup channel")]ulong id)
            {
                settings.SetData(EVENT_SIGNUP_KEY, id);
                await ReplyAsync("Bound signup channel!");
            }

            [Command("main signup emote")]
            public async Task SetMainSignupEmote(ulong id)
            {
                settings.SetData(MAIN_SGEMOTE_KEY, id);
                await ReplyAsync("Set main signup emote!");
            }

            [Command("extra signup emote")]
            public async Task AddExtraSignupEmote(ulong id)
            {
                HashSet<ulong> allEmotes = settings.GetDataOrDefault(SIGNUP_EMOTE_KEY, new List<ulong>()).ToHashSet();
                allEmotes.Add(id);
                settings.SetData(SIGNUP_EMOTE_KEY, allEmotes);
                await ReplyAsync("Added extra signup emote!");
            }
        }
    }
}
