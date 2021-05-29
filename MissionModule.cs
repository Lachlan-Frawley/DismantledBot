using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using System;

namespace DismantledBot
{
    public enum AllChannels
    {
        Calpheon,
        Mediah,
        Arsha,
        Kamasylvia,
        Balenos,
        Valencia,
        Velia,
        Serendia
    }

    [Group("mission")]
    public class MissionModule : ModuleBase<SocketCommandContext>
    {
        public static ModuleSettingsManager<MissionModule> settings = ModuleSettingsManager<MissionModule>.MakeSettings();

        private static TimerPlus resetTimer; 

        public static void OnBotStart()
        {
            resetTimer = new TimerPlus()
            {
                AutoReset = false
            };

            DateTime time = DateTime.Now;
            if (time.Hour > 10)
                time = new DateTime(time.Year, time.Month, time.Day + 1, 10, 0, 0);
            else
                time = new DateTime(time.Year, time.Month, time.Day, 10, 0, 0);

            resetTimer.Interval = (time - DateTime.Now).TotalMilliseconds;
            resetTimer.Elapsed += (x, y) =>
            {
                resetTimer.Interval = new TimeSpan(24, 0, 0).TotalMilliseconds;
                resetTimer.Start();
                // Clear missions on reset
                settings.Clear();
            };

            resetTimer.Start();
        }

        [Command("debug")]
        [IsCreator]
        public async Task MissionDebug()
        {
            Console.WriteLine("Current Missions: ");
            foreach(KeyValuePair<string, string> o in settings.GetAllDataAsString())
            {
                string[] split = o.Key.Split('_');
                string id = o.Value;
                string server = split[0];
                string channel = split[1];

                Console.WriteLine($"Server: {server} {channel}, ID: {id}");
            }
            Console.WriteLine();
            await ReplyAsync($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
        }

        [Command("register")]
        [Summary("Registers a sea monster mission")]
        [HasRole(Functions.Names.MEMBER_ROLE_FUNC)]
        public async Task RegisterSeaMonsters(
            [Summary("The channel the mission is on")]AllChannels channel, 
            [Summary("The channel number the mission is on")]int channelNumber, 
            [Summary("The number of monsters to kill")] int count)
        {
            SocketTextChannel missionChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_MISSION_KEY));
            IUserMessage msg = await missionChannel.SendMessageAsync($"x{count} {channel}{channelNumber}");

            settings.SetData($"{channel}_{channelNumber}", msg.Id);
        }

        [Command("complete")]
        [Summary("Completes a sea monster mission")]
        public async Task CompleteSeaMonsters(
            [Summary("The channel the mission is on")] AllChannels channel,
            [Summary("The channel number the mission is on")] int channelNumber)
        {
            ulong mid = settings.GetDataOrDefault<ulong>($"{channel}_{channelNumber}", 0);
            if(mid == 0)
            {
                await ReplyAsync("Couldn't find mission to complete!");
            }

            SocketTextChannel missionChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_MISSION_KEY));
            IMessage missionMessage = await missionChannel.GetMessageAsync(mid);
            IEmote reactEmote = await Context.Guild.GetEmoteAsync(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_MISEMOJI_KEY));           
            await missionMessage.AddReactionAsync(reactEmote);
            await ReplyAsync("Mission marked as complete!");
        }
    }
}
