using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NodaTime;

namespace DismantledBot
{
    public static class WarUtility
    {
        private static TimerPlus TimeUntilNextWar, TimeUntilWarEnd;
        public static bool IsRecordingWar { get; private set; } = false;

        private static EventData currentWar;
        private static HashSet<ulong> inChannel;

        public static void Initialize()
        {
            HashSet<EventData> foundEvents = CoreProgram.database.GetRows(new EventData.Comparer());
            var now = DateTime.UtcNow;
            var minTimespan = foundEvents.Min(x => x.EventDate - now);
            var minTime = minTimespan.TotalMilliseconds - TimeSpan.FromMinutes(30).TotalMilliseconds;
            currentWar = foundEvents.ToList().Find(x => x.EventDate - now == minTimespan);
            var endTime = TimeSpan.FromHours(2.5).TotalMilliseconds;

            if (minTime < 0)
            {
                endTime += minTime;
                minTime = 0;
            }
            if (endTime < 0)
                endTime = 0;

            TimeUntilNextWar = new TimerPlus()
            {
                AutoReset = false,
                Interval = minTime
            };
            TimeUntilWarEnd = new TimerPlus()
            {
                AutoReset = false,
                Interval = endTime
            };

            TimeUntilNextWar.Elapsed += TimeUntilNextWar_Elapsed;
            TimeUntilWarEnd.Elapsed += TimeUntilWarEnd_Elapsed;
            TimeUntilNextWar.Start();
            CoreProgram.logger.Write2(Logger.DEBUG, $"Started wartimer, {TimeUntilNextWar.TimeLeft} ms remaining");
        }

        public static void AddUserIntoAttendance(ulong id)
        {
            if (!IsRecordingWar)
                return;
            if (inChannel == null)
                inChannel = GetMembersInWarChannels();
            inChannel.Add(id);
        }

        public static void TimeUntilNextWar_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TimeUntilNextWar.Stop();
            inChannel = GetMembersInWarChannels();
            IsRecordingWar = true;
            CoreProgram.logger.Write2(Logger.DEBUG, "Now recording war!!");
        }

        public async static void TimeUntilWarEnd_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TimeUntilWarEnd.Stop();
            IsRecordingWar = false;
            HashSet<GuildMember> attendedMembers = CoreProgram.database.GetRows(new GuildMember.Comparer(), x => inChannel.Contains(x.DiscordID));
            HashSet<GuildTeams> teams = CoreProgram.database.GetRows(new GuildTeams.Comparer());
            HashSet<TeamMember> teamData = CoreProgram.database.GetRows(new TeamMember.Comparer());

            Dictionary<GuildTeams, List<GuildMember>> attendanceData = new Dictionary<GuildTeams, List<GuildMember>>();
            foreach(GuildMember user in attendedMembers)
            {
                try
                {
                    GuildTeams userTeam = teams.Find(x => x.TeamID == teamData.Find(x => x.DiscordID == user.DiscordID).TeamID);
                    List<GuildMember> teamList = attendanceData.GetValueOrDefault(userTeam, new List<GuildMember>());
                    teamList.Add(user);
                    attendanceData[userTeam] = teamList;
                } catch(Exception ex)
                {
                    CoreProgram.logger.Write2(Logger.MAJOR, ex.Message);
                }
            }

            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Nodewar Attendance";
            DateTime warDT = currentWar.EventDate;
            ZonedDateTime zdt = new ZonedDateTime(Instant.FromUtc(warDT.Year, warDT.Month, warDT.Day, warDT.Hour, warDT.Minute), DateTimeZone.Utc);
            DateTime realDT = Utilities.ConvertDateTimeToDifferentTimeZone(zdt.LocalDateTime, zdt.Zone.Id, DateTimeZoneProviders.Tzdb["CST6CDT"].Id).ToDateTimeUnspecified();
            builder.Description = $"{realDT.DayOfWeek}, {realDT.ToShortDateString()}";
            builder.AddField("Team Attendance", string.Join("\n", attendanceData.Select(x => $"{x.Key.TeamName}: {x.Value.Count}")));
            builder.AddField("Attendance", string.Join("\n", attendedMembers.Select(x => x.Name)));

            SocketTextChannel warDiscussion = CoreProgram.BoundGuild.GetTextChannel(SettingsModule.settings.GetData<ulong>(SettingsModule.WAR_DISCUSSN_KEY));
            await warDiscussion.SendMessageAsync(embed: builder.Build());
        }      

        public static HashSet<ulong> GetMembersInWarChannels()
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketCategoryChannel warCat = guild.GetCategoryChannel(SettingsModule.settings.GetData<ulong>(SettingsModule.WAR_CATEGORY_KEY));
            IEnumerable<SocketVoiceChannel> voiceChannels = warCat.Channels.Select(x => x as SocketVoiceChannel).Where(x => x != null);
            var allUsers = voiceChannels.SelectMany(x => x.Users).Where(x => x != null);
            return allUsers.Select(x => x.Id).ToHashSet();
        }

        public static void HandleNewEventMessage(SocketMessage message)
        {
            HashSet<EventData> currentEvents = CoreProgram.database.GetRows(new EventData.Comparer());
            EventData target = currentEvents.Find(x => x.MessageID == message.Id);
            if (target != null)
                return;
            if (!HandleEventCreation(message, out EventData newEvent) || newEvent == null)
            {
                CoreProgram.logger.Write2(Logger.WARNING, "Failed to create event from message! (Note: Event might not be a war)");
                return;
            }

            CoreProgram.database.InsertSingle(newEvent);
        }

        public static void HandleDeletedEventMessage(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            HashSet<EventData> currentEvents = CoreProgram.database.GetRows(new EventData.Comparer());
            EventData target = currentEvents.Find(x => x.MessageID == message.Id);
            if (target == null)
                return;

            CoreProgram.database.DeleteSingle(target, "EventDate");
            PreviousEventData archiveData = new PreviousEventData(target);
            CoreProgram.database.InsertSingle(archiveData);
            Initialize();
        }

        public static void HandleSignupReact(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            Emote emote = reaction.Emote as Emote;
            if (emote == null)
                return;

            ulong mainEmote = SettingsModule.settings.GetData<ulong>(SettingsModule.MAIN_SGEMOTE_KEY);
            HashSet<ulong> extraEmotes = SettingsModule.settings.GetList(SettingsModule.SIGNUP_EMOTE_KEY, new HashSet<ulong>());
            var dataList = CoreProgram.database.GetRows(new CurrentEventSignupData.Comparer(), x => x.DiscordID == reaction.UserId);
            var eDataList = CoreProgram.database.GetRows(new EventData.Comparer(), x => x._MessageID == message.Id);

            CurrentEventSignupData data = dataList.Count == 0 ? null : dataList.First();
            EventData eData = eDataList.Count == 0 ? null : eDataList.First();

            if (eData == null)
            {
                CoreProgram.logger.Write2(Logger.ERROR, $"No event date for MID: [{message.Id}]");
                return;
            }

            if (emote.Id == mainEmote)
            {
                if(data == null)
                {                    
                    CurrentEventSignupData newSignup = new CurrentEventSignupData()
                    {
                        DiscordID = reaction.UserId,     
                        EventDate = eData.EventDate
                    };
                    CoreProgram.database.InsertAttendance(newSignup);
                } else
                {
                    CoreProgram.database.DeleteSingle(data, "DiscordID", "EventDate");
                }
            } else if(extraEmotes.Contains(emote.Id))
            {
                if (data == null)
                    return;
                else
                    CoreProgram.database.DeleteSingle(data, "DiscordID", "EventDate");
            }
        }

        public static bool HandleEventCreation(IMessage message, out EventData eventData)
        {
            eventData = null;
            if (message == null || message.Embeds.Count == 0)
                return false;

            Regex timeRegex = new Regex(WarCommands.TIME_EXTRACT_REGEX);
            Regex nwRegex = new Regex(WarCommands.NODEWAR_REGEX);

            IEmbed embed = message.Embeds.First();
            if (nwRegex.Matches(embed.Title).Count == 0 || embed.Fields.Length == 0)
                return false;

            try
            {
                EmbedField timeField = embed.Fields[0];
                Match timeData = timeRegex.Match(timeField.Value);
                GroupCollection groups = timeData.Groups;
                Group foundGroup;

                // God why
                groups.TryGetValue("DAY", out foundGroup);
                string day = foundGroup.Value;
                groups.TryGetValue("MONTH", out foundGroup);
                string month = foundGroup.Value;
                groups.TryGetValue("DNUM", out foundGroup);
                string dayNumber = foundGroup.Value;
                groups.TryGetValue("YEAR", out foundGroup);
                string year = foundGroup.Value;
                groups.TryGetValue("HOUR", out foundGroup);
                string hour = foundGroup.Value;
                groups.TryGetValue("MINUTE", out foundGroup);
                string minutes = foundGroup.Value;
                groups.TryGetValue("ZONE", out foundGroup);
                string timeZone = foundGroup.Value;
                groups.TryGetValue("REL", out foundGroup);
                string relativeTimeZone = foundGroup.Value;
                groups.TryGetValue("OFSS", out foundGroup);
                string offsetSign = foundGroup.Value;
                groups.TryGetValue("OFSN", out foundGroup);
                string offsetNumber = foundGroup.Value;

                int dyear = int.Parse(year);
                int dmonth = DateTime.ParseExact(month, "MMM", CultureInfo.CurrentCulture).Month;
                int dday = int.Parse(dayNumber);
                int dhour = int.Parse(hour);
                int dmin = int.Parse(minutes);

                int warOffsetTime = int.Parse(offsetNumber);
                if (offsetSign.Equals("-"))
                    warOffsetTime *= -1;

                ZonedDateTime zdt = new ZonedDateTime(new LocalDateTime(dyear, dmonth, dday, dhour, dmin), DateTimeZoneProviders.Tzdb["CST6CDT"], Offset.FromHours(warOffsetTime));

                DateTime startTime = zdt.ToDateTimeUtc();
                string name = embed.Title;
                EventData eData = new EventData()
                {
                    EventName = name,
                    EventDate = startTime,
                    _MessageID = message.Id
                };
                eventData = eData;
                return true;
            }
            catch
            {
                return true;
            }
        }
    }

    [Group("war")]
    public sealed class WarCommands : ModuleBase<SocketCommandContext>
    {
        // Regexs
        public const string TIME_EXTRACT_REGEX = @"(?<DAY>[A-Za-z]*) (?<MONTH>[A-Za-z]*) (?<DNUM>[0-9]*)(th|st|nd|rd|), (?<YEAR>[0-9]*) ⋅ ((?<HOUR>[0-9]*):(?<MINUTE>[0-9]*)( - ([0-9]*):([0-9]*))?) (?<ZONE>[A-Z]*) \((?<REL>[A-Z]*)(?<OFSS>\+|-)(?<OFSN>[0-9]*)\)";
        public const string NODEWAR_REGEX = @"(n|N)(o|O)(d|D)(e|E) (w|W)(a|A)(r|R)";

        [RequireContext(ContextType.Guild)]
        public sealed class ServerOnlyCommands : ModuleBase<SocketCommandContext>
        {
            [Command("force end")]
            [HasRole(Functions.Names.GET_OFFICER_FUNC)]
            public Task ForceEndWar()
            {
                WarUtility.TimeUntilWarEnd_Elapsed(this, null);
                return Task.CompletedTask;
            }

            [Command("force start")]
            [IsBotAdmin]
            public Task ForceStartWar()
            {
                WarUtility.TimeUntilNextWar_Elapsed(this, null);
                return Task.CompletedTask;
            }
        }      

        [Command("calibrate")]
        [RequireContext(ContextType.DM)]
        [IsBotAdmin]
        public async Task AttemptCalibration()
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketTextChannel eventSignupChannel = guild.GetTextChannel(SettingsModule.settings.GetData<ulong>(SettingsModule.EVENT_SIGNUP_KEY));
            var allMessages = await eventSignupChannel.GetMessagesAsync().FlattenAsync();

            int successCount = 0;
            int actualWars = 0;
            foreach (IMessage message in allMessages)
            {
                if (WarUtility.HandleEventCreation(message, out EventData data))
                    actualWars++;

                if(data != null)
                {
                    successCount++;
                    CoreProgram.database.InsertSingle(data);
                }
            }

            await ReplyAsync($"Calibrated for {successCount}/{actualWars} wars!");
        }

        [Command("next")]
        [HasRole(Functions.Names.GET_GUILD_MEMBER_FUNC)]
        public async Task GetNextWar()
        {
            HashSet<EventData> foundEvents = CoreProgram.database.GetRows(new EventData.Comparer());
            await ReplyAsync($"Found {foundEvents.Count} wars");
            var now = DateTime.UtcNow;
            var minTime = foundEvents.Min(x => x.EventDate - now);
            var nextWar = foundEvents.ToList().Find(x => x.EventDate - now == minTime);
            await ReplyAsync($"Next War: {nextWar.EventName}");
            string untilNextFormatted = string.Format("{0:%d} day(s), {0:%h} hours, {0:%m} minutes", minTime);
            await ReplyAsync($"Time until next war: {untilNextFormatted}");
            TimerPlus nextTimer = typeof(WarUtility).GetField("TimeUntilNextWar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null) as TimerPlus;
            if(nextTimer == null)
            {
                CoreProgram.logger.Write2(Logger.WARNING, "Error finding timer!");
                return;
            }
            double toNext = nextTimer.TimeLeft;
            untilNextFormatted = string.Format("{0:%d} day(s), {0:%h} hours, {0:%m} minutes", new TimeSpan(0, 0, 0, 0, (int)toNext));
            await ReplyAsync($"Internal Clock Remaining Time: {untilNextFormatted}");
        }
    }
}
