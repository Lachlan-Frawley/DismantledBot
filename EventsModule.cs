using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NodaTime;

namespace DismantledBot
{
    public struct EventDataObject
    {
        public ulong? ExistingMessageID;
        public ulong? MessageChannelID;
        public string EventName;
        public string EventDescription;
        public int? MaxParticipants;
        public EventTimeObject EventTime;
        public EventScheduleObject EventSchedule;
        public long? EventLengthSeconds;
        public ulong AcceptedEmoteID;
    }
    
    public struct EventTimeObject
    {
        public string TimeZone;
        public TimeSpan EventTime;
    }

    public struct EventScheduleObject
    {
        [Flags]
        public enum Weekday
        {
            Monday = 1, 
            Tuesday = 2, 
            Wednesday = 4,
            Thursday = 8,
            Friday = 16, 
            Saturday = 32, 
            Sunday = 64,
            ALL = 128
        }

        [Flags]
        public enum FourWeekRepeat
        {
            Week0 = 1, 
            Week1 = 2, 
            Week2 = 4, 
            Week3 = 8,
            ALL = 16
        }

        public Weekday ApplicableDays;
        public FourWeekRepeat WeekRepetition;
    }

    [Group("event")]
    public class EventsModule : ModuleBase<SocketCommandContext>
    {
        public static Embed CreateEmbed(EventDataObject eventData, Emote acceptedEmote)
        {
            EmbedBuilder builder = new EmbedBuilder();

            builder.Title = eventData.EventName;
            builder.Description = eventData.EventDescription;

            builder.AddField("Time", "Time: TODO");
            string maxSignupCount = eventData.MaxParticipants.HasValue ? $"/{eventData.MaxParticipants}" : string.Empty;

            int signupCount = CoreProgram.database.QueryEventSignupCount(eventData.MessageChannelID);
            List<string> signupNames = CoreProgram.database.QueryEventSignupNames(eventData.MessageChannelID);
            string signupFieldValue = string.Join(",\n", signupNames);
            if (string.IsNullOrEmpty(signupFieldValue))
                signupFieldValue = "None!";
            string emote = acceptedEmote.ToString();

            try
            {
                builder.AddField($"{emote} Accepted ({signupCount}{maxSignupCount})", signupFieldValue);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            builder.WithFooter("Repeats: TODO");

            return builder.Build();
        }

        [Command("create")]
        [IsMessageNotDM]
        [IsCreator]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        [Summary("Creates a new event")]
        public async Task CreateEvent()
        {
            // Currently a debug thing

            EventDataObject eventData = new EventDataObject()
            {
                AcceptedEmoteID = 756450576723607612,
                EventDescription = "An event for testing",
                EventName = "Test Event",
                MaxParticipants = 40,
                EventLengthSeconds = null,
                ExistingMessageID = null,
                MessageChannelID = null,
                EventTime = new EventTimeObject()
                {
                    TimeZone = "CDT",
                    EventTime = new TimeSpan(20, 0, 0)
                },
                EventSchedule = new EventScheduleObject()
                {
                    ApplicableDays = EventScheduleObject.Weekday.Friday | EventScheduleObject.Weekday.Tuesday | EventScheduleObject.Weekday.Sunday,
                    WeekRepetition = EventScheduleObject.FourWeekRepeat.Week0 | EventScheduleObject.FourWeekRepeat.Week2 | EventScheduleObject.FourWeekRepeat.Week3
                }
            };

            Emote acceptedEmote = await Context.Guild.GetEmoteAsync(eventData.AcceptedEmoteID);
            Embed eventEmbed = CreateEmbed(eventData, acceptedEmote);
            IUserMessage mRef = await ReplyAsync("Hehe Test", embed: eventEmbed);
            await mRef.AddReactionAsync(acceptedEmote);

            try
            {
                CoreProgram.database.CreateEvent(eventData);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                await mRef.DeleteAsync();
                await ReplyAsync("Database insertion failed...");
                return;
            }
        }
    }
}
