using Discord.Commands;
using System.Threading.Tasks;

namespace DismantledBot
{
    public sealed class SettingsModule : ModuleBase<SocketCommandContext>
    {
        public static readonly ModuleSettingsManager<SettingsModule> settings = ModuleSettingsManager<SettingsModule>.MakeSettings();

        [Group("set")]
        [IsBotAdmin]
        public sealed class SetSegment : ModuleBase<SocketCommandContext>
        {
            [Command("owner")]
            [Summary("Sets the bound servers owner")]
            public async Task BindOwner([Summary("The ID of the owner")] ulong id)
            {
                settings.SetData(Functions.Names.GET_OWNER_FUNC, id);
                await ReplyAsync("Owner bound!");
            }
        }       
    }
}
