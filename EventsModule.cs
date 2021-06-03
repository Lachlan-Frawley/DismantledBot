using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using NodaTime;

namespace DismantledBot
{
    public sealed class EventData
    {
        public ulong? ExistingMessageID;

        public string EventName;
        public string EventDescription;

        public ZonedDateTime StartTime;
        public TimeSpan? EventLength;

        public ulong AcceptedEmoteID;
    }

    [Group("event")]
    public class EventsModule : ModuleBase<SocketCommandContext>
    {
        [Command("create")]
        [IsMessageNotDM]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        [Summary("Creates a new event")]
        public async Task CreateEvent()
        {
            await ReplyAsync("Hello world!");
        }
    }
}
