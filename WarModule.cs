using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System;
using System.Globalization;
using System.Timers;
using System.Reflection;
using NodaTime;
using NodaTime.Extensions;

namespace DismantledBot
{
    // War timing utility
    public static class WarUtility
    {
        // Flag if a war is currently being recorded
        public static bool IsRecordingWar { get; private set; } = false;

        // Timers
        private static TimerPlus UntilWarTimer;
        private static TimerPlus WarTimer;
        private static TimerPlus AttendanceTimer;

        // Required data
        public static List<string> Attendance { get; private set; }
        public static List<string> SignupList { get; private set; }
        public static DayOfWeek WarDay { get; private set; }
        public static DateTime WarDT { get; private set; }
        public static int WarIndex { get; private set; }

        // Flag if this is a test (wont' change saved war schedule if true)
        public static bool IsTest = false;

        public static List<IGuildUser> currentUsers;

        // Called when the bot starts to setup timers
        public static void OnBotStart()
        {
            // Clear everything
            UntilWarTimer = null;
            WarTimer = null;
            AttendanceTimer = null;
            Attendance = null;
            SignupList = null;
            WarIndex = -1;

            // Get next war
            TimeSpan shortestSpan = TimeSpan.MaxValue;
            for(int i = 0; i < WarModule.settings.GetData<int>(WarModule.WAR_COUNT_KEY); i++)
            {
                DateTime time = WarModule.settings.GetData<DateTime>($"{WarModule.SAVED_WAR_PREFIX}{i}");
                TimeSpan contender = time - DateTime.UtcNow;
                if (contender < shortestSpan)
                {
                    shortestSpan = contender;
                    WarDT = time;
                    WarDay = WarDT.DayOfWeek;                    
                    WarIndex = i;
                }
            }

            ZonedDateTime zdt = new ZonedDateTime(Instant.FromDateTimeUtc(WarDT), DateTimeZoneProviders.Tzdb["CST6CDT"]);
            WarDay = zdt.DayOfWeek.ToDayOfWeek();
            

            // Go back 30 minutes from war start
            shortestSpan = shortestSpan.Add(new TimeSpan(0, -30, 0));

            UntilWarTimer = new TimerPlus()
            {
                AutoReset = false,
                Interval = shortestSpan.TotalMilliseconds <= 0 ? 100 : shortestSpan.TotalMilliseconds               
            };

            UntilWarTimer.Elapsed += UntilWarTimer_Elapsed;
            UntilWarTimer.Start();
        }

        // Called 30 mins before nodewar starts
        private static async void UntilWarTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            WarModule.settings.SetData(WarModule.LAST_WAR_CHAN_KEY, (WarModule.WarChannel)WarModule.settings.GetDataOrDefault<int>(WarModule.WAR_CHAN_KEY, (int)WarModule.WarChannel.NULL));
            WarModule.settings.SetData(WarModule.WAR_CHAN_KEY, null);
            WarModule.settings.SetData(WarModule.CHAN_SET_KEY, false);

            UntilWarTimer.Stop();

            // Get selected event signup list
            Regex nwRegex = new Regex(WarModule.NODEWAR_REGEX);
            SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
            SocketTextChannel eventChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
            List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
            IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).Where(x => x.Embeds.Any(y => y.Title.Contains(WarDay.ToString(), StringComparison.InvariantCultureIgnoreCase))).First();
            IEmbed targetEmbed = selectedMessage.Embeds.First();
            EmbedField targetField = targetEmbed.Fields[2];

            currentUsers = (await guild.GetUsersAsync().FlattenAsync()).ToList();

            // Get all signed up names and everyone in channel
            SignupList = WarModule.ExtractNamesFromEmbed(targetField.Value);

            // Setup timers for whole war (2hrs, 30 mins)
            WarTimer = new TimerPlus()
            {
                AutoReset = false,
                Interval = new TimeSpan(2, 30, 0).TotalMilliseconds
            };

            // Records attendance every 30 seconds
            AttendanceTimer = new TimerPlus()
            {
                AutoReset = true,
                Interval = new TimeSpan(0, 0, 30).TotalMilliseconds
            };

            WarTimer.Elapsed += WarTimer_Elapsed;
            AttendanceTimer.Elapsed += AttendanceTimer_Elapsed;

            WarTimer.Start();
            AttendanceTimer.Start();
            IsRecordingWar = true;            

            // Send message to war discussion channel           
            SocketTextChannel wardisc = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARDISC_KEY));

            await wardisc.SendMessageAsync($"Now recording attendance for {WarDay} Nodewar [{WarDT.ToShortDateString()}]");
        }

        // Called to mark attendance
        private static void AttendanceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Record who is currently in the channels
            if (Attendance == null)
                Attendance = new List<string>();

            SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
            List<string> names = WarModule.ExtractWarChannelNames(guild);

            // Add names and remove duplicates
            Attendance.AddRange(names);
            Attendance = Attendance.Distinct().ToList();
        }

        // Called when war has ended
        private static async void WarTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Print war data
            IsRecordingWar = false;
            if(AttendanceTimer != null)
                AttendanceTimer.Stop();
            if(WarTimer != null)
                WarTimer.Stop();
            // Take attendance one more time
            AttendanceTimer_Elapsed(null, null);

            // Work out what message we need to read signup from
            Regex nwRegex = new Regex(WarModule.NODEWAR_REGEX);
            SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
            SocketTextChannel eventChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
            List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
            IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).Where(x => x.Embeds.Any(y => y.Title.Contains(WarDay.ToString(), StringComparison.InvariantCultureIgnoreCase))).First();
            IEmbed targetEmbed = selectedMessage.Embeds.First();
            EmbedField targetField = targetEmbed.Fields[2];

            // Get all signed up names and everyone in channel
            List<string> namesInChannels = Attendance;

            // What names are common between the attendance and signup
            List<string> commonNames = SignupList.Intersect(namesInChannels).ToList();
            // Who has attended but not signed up?
            List<string> attendedButNotSignedUp = namesInChannels.Except(SignupList).ToList();

            List<string> attended = Utilities.MatchNames(commonNames.Union(attendedButNotSignedUp), guild);
            List<string> missingName = Utilities.MatchNames(SignupList.Except(namesInChannels), guild);
          
            // Build an embed for the message
            EmbedBuilder builder = new EmbedBuilder();
            builder.Description = $"{WarDay} War Attendance [{WarDT.ToShortDateString()}]";
            if (attended.Count != 0)
                builder.AddField("Attendance", string.Join(",\n", attended), true);
            if (missingName.Count != 0)
                builder.AddField("Missing", string.Join(",\n", missingName), true);

            // Send attendance data to war discussion
            SocketTextChannel wardiscChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARDISC_KEY));
            await wardiscChannel.SendMessageAsync(embed: builder.Build());

            // Don't push war time forward if this is a test
            if(!IsTest)
            {
                // Setup timers again
                WarModule.settings.SetData($"{WarModule.SAVED_WAR_PREFIX}{WarIndex}", WarDT.AddDays(7));
                OnBotStart();
            }

            IsTest = false;
        }
    }

    [Group("war")]
    public class WarModule : ModuleBase<SocketCommandContext>
    {
        public enum WarChannel
        {
            Balenos,
            Serendia,
            Calpheon,
            Mediah,
            Valencia,
            NULL
        }

        // Regexs
        public const string TIME_EXTRACT_REGEX = @"(?<DAY>[A-Za-z]*) (?<MONTH>[A-Za-z]*) (?<DNUM>[0-9]*)(th|st|nd|rd), (?<YEAR>[0-9]*) .* (?<HOUR>[0-9]*):(?<MINUTE>[0-9]*) (?<ZONE>[A-Z]*) \((?<REL>[A-Z]*)(?<OFSS>\+|-)(?<OFSN>[0-9]*)\)";
        public const string NODEWAR_REGEX = @"(n|N)(o|O)(d|D)(e|E) (w|W)(a|A)(r|R)";

        // Settings keys and prefixs
        public const string SAVED_WAR_PREFIX = "WAR_";
        public const string SAVED_TIME_PREFIX = "WTO_";
        public const string WAR_COUNT_KEY = "WCK";
        public const string SERVER_ID_KEY = "SIDK";
        public const string WAR_CHAN_KEY = "WCH";
        public const string CHAN_SET_KEY = "WCSK";
        public const string LAST_WAR_CHAN_KEY = "LWCK";

        public static ModuleSettingsManager<WarModule> settings = ModuleSettingsManager<WarModule>.MakeSettings();

        // Extract names from Apollo embeded list
        public static List<string> ExtractNamesFromEmbed(string input)
        {
            input = input.Substring(4);
            string[] splits = input.Split('\n');

            return splits.Select(x => x.Replace("\\", "")).ToList();
        }

        // Extract names from all war channels
        public static List<string> ExtractWarChannelNames(SocketGuild guild)
        {
            List<SocketVoiceChannel> warChannels = guild.GetCategoryChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARCAT_KEY)).Channels.Where(x => x is SocketVoiceChannel).Select(x => x as SocketVoiceChannel).ToList();

            /*List<SocketGuildUser> warUsers = warChannels.SelectMany(x => x.Users).ToList();
            return warUsers.Select(x => string.IsNullOrEmpty(x.Nickname) ? x.Username : x.Nickname).ToList();*/
            /*List<IGuildUser> currentUsers = (await guild.GetUsersAsync().FlattenAsync()).ToList();
            return currentUsers.Where(x => warChannels.Any(y => x.VoiceChannel.Id == y.Id)).Select(x => string.IsNullOrEmpty(x.Nickname) ? x.Username : x.Nickname).ToList();*/
            if (WarUtility.currentUsers == null)
                return new List<string>();
            return WarUtility.currentUsers.Where(x => x != null && x.VoiceChannel != null && warChannels.Any(y => x.VoiceChannel.Id == y.Id)).Select(x => string.IsNullOrEmpty(x.Nickname) ? x.Username : x.Nickname).ToList();
        }

        // Get guild member names
        public static List<(string, string)> GetAllGuildMembers(SocketGuild guild)
        {
            SocketRole selectedRole = guild.GetRole(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_GMEMBER_KEY));
            return guild.Users/*.Where(x => x.Roles.Contains(selectedRole))*/.Select(x => (string.IsNullOrEmpty(x.Nickname) ? "" : x.Nickname, x.Username)).ToList();
        }

        [Command("extras")]
        [Summary("Lists those who are in the war channel but not signed up for war")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task ExtraAttendance()
        {
            if(!WarUtility.IsRecordingWar)
            {
                await UserExtensions.SendMessageAsync(Context.User, "There is currently no war!");
                return;
            }
            
            List<string> currentSignup = new List<string>(WarUtility.SignupList);
            List<string> currentChannelMembers = ExtractWarChannelNames(Context.Guild);
            currentSignup = Utilities.MatchNames(currentSignup, Context.Guild);
            List<string> notSignedUp = currentChannelMembers.Except(currentSignup).ToList();

            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Unregistered Nodewar Members";
            builder.Description = WarUtility.WarDT.ToShortDateString();
            builder.AddField("Names", string.Join(",\n", notSignedUp));

            await UserExtensions.SendMessageAsync(Context.User, embed: builder.Build());
        }

        // Forceably start recording war
        [Command("force_start")]
        [Summary("Force starts the next war")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task ForceWarStart()
        {
            await ReplyAsync("Forcing war start...");
            WarUtility.OnBotStart();          
            typeof(WarUtility).GetMethod("UntilWarTimer_Elapsed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this, null });
        }

        // Forceably end war
        [Command("force_end")]
        [Summary("Forceably ends current war, takes attendance, and shifts the war timer ahead one week")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task ForceWarEnd([Summary("If true, does not push the current war timer ahead one week")]bool isTest = false)
        {
            await ReplyAsync("Forcing war end...");
            // Make sure war time isnt pushed forward if this is a test
            if (isTest)
                WarUtility.IsTest = true;
            typeof(WarUtility).GetMethod("WarTimer_Elapsed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this, null });            
        }

        // Gather nodewar event data and save it
        // Good luck working out what happens here
        [Command("calibrate", RunMode = RunMode.Async)]
        [Summary("Reads the event data from the bound event signup channel and saves it for reference")]
        [IsUser(Functions.Names.CREATOR_ID_FUNC, Functions.Names.GET_OWNER_ID_FUNC)]
        public async Task CalibrateWarTimes()
        {
            Regex timeRegex = new Regex(TIME_EXTRACT_REGEX);
            Regex nwRegex = new Regex(NODEWAR_REGEX);

            SocketTextChannel eventChannel = Context.Guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
            List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();

            int warNum = 0;
            foreach (IMessage message in eventMessages)
            {
                // TODO
                if (message.Embeds.Count == 0)
                    continue;

                IEmbed embed = message.Embeds.First();

                // TODO
                if (nwRegex.Matches(embed.Title).Count == 0 || embed.Fields.Length == 0)
                    continue;

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

                    ZonedDateTime zdt = new ZonedDateTime(new LocalDateTime(dyear, dmonth, dday, 20, 0), DateTimeZoneProviders.Tzdb["CST6CDT"], Offset.FromHours(warOffsetTime));

                    DateTime startTime = zdt.ToDateTimeUtc();
                    TimeSpan untilNw = startTime - DateTime.UtcNow;
                    TimeSpan nwTimeTicks = new TimeSpan(2, 0, 0);
                    while (untilNw.Ticks <= -nwTimeTicks.Ticks)
                    {
                        startTime = startTime.AddDays(7);
                        untilNw = startTime - DateTime.UtcNow;
                    }

                    settings.SetData($"{SAVED_WAR_PREFIX}{warNum++}", startTime);
                } catch
                {
                    continue;
                }
            }

            settings.SetData(WAR_COUNT_KEY, warNum);
            settings.SetData(SERVER_ID_KEY, Context.Guild.Id);
            WarUtility.OnBotStart();
            await ReplyAsync($"Calibrated for [{warNum}] Nodewar(s)");
        }
        
        // Time until next nodewar
        [Command("next")]
        [Summary("Prints the time until the next nodewar, and what channel it is on (if thats known)")]
        public async Task FindNextWar()
        {
            TimerPlus untilNext = typeof(WarUtility).GetField("UntilWarTimer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as TimerPlus;
            double toNext = untilNext != null ? untilNext.TimeLeft : -1;
            // Format it to look pretty :)
            string untilNextFormatted = string.Format("{0:%d} day(s), {0:%h} hours, {0:%m} minutes", new TimeSpan(0, 0, 0, 0, (int)toNext));
            string warChannel = settings.GetData<bool>(CHAN_SET_KEY) ? $", Channel: {(WarChannel)settings.GetDataOrDefault<int>(WAR_CHAN_KEY, (int)WarChannel.NULL)} 1" : "";
            if (settings.GetDataOrDefault(WAR_CHAN_KEY, (int)WarChannel.NULL) == (int)WarChannel.NULL)
                warChannel = ", Channel is not set";
            await ReplyAsync($"Time until next War: {untilNextFormatted}{warChannel}");
        }

        // Set next nw channel
        [Command("channel")]
        [Summary("Sets the nodewar channel")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task SetWarChannel([Summary("Valid Channels: Balenos, Serendia, Calpheon, Media, Valencia (case insensitive)")]WarChannel channel)
        {
            settings.SetData(CHAN_SET_KEY, true);
            settings.SetData(WAR_CHAN_KEY, channel);
            await ReplyAsync($"Set next war channel to [{channel}]");
        }

        [Command("add_player")]
        [Summary("Adds a player")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task AddPlayer([Summary("Name of the player to add")]string name)
        {
            if (!WarUtility.Attendance.Contains(name))
                WarUtility.Attendance.Add(name);
            await ReplyAsync($"Added player {name} to war attendance");
        }

        // Debug commands
        [Group("debug")]
        [IsCreator]
        public class DebugModule : ModuleBase<SocketCommandContext>
        {
            [Command("sample2")]
            public async Task SampleWar2()
            {
                if(!WarUtility.IsRecordingWar)
                {
                    await ReplyAsync("Invalid timing");
                    return;
                }

                // Setup some stuff
                List<string> SignupList = new List<string>(WarUtility.SignupList);
                DayOfWeek WarDay = WarUtility.WarDay;
                DateTime WarDT = WarUtility.WarDT;

                // Work out what message we need to read signup from
                Regex nwRegex = new Regex(WarModule.NODEWAR_REGEX);
                SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
                SocketTextChannel eventChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
                List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
                IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).Where(x => x.Embeds.Any(y => y.Title.Contains(WarDay.ToString(), StringComparison.InvariantCultureIgnoreCase))).First();
                IEmbed targetEmbed = selectedMessage.Embeds.First();
                EmbedField targetField = targetEmbed.Fields[2];

                // Get all signed up names and everyone in channel
                List<string> namesInChannels = new List<string>(WarUtility.Attendance);

                // What names are common between the attendance and signup
                List<string> commonNames = SignupList.Intersect(namesInChannels).ToList();
                // Who has attended but not signed up?
                List<string> attendedButNotSignedUp = namesInChannels.Except(SignupList).ToList();

                List<string> attended = Utilities.MatchNames(commonNames.Union(attendedButNotSignedUp), guild);
                List<string> missingName = Utilities.MatchNames(SignupList.Except(namesInChannels), guild);

                // Build an embed for the message
                EmbedBuilder builder = new EmbedBuilder();
                builder.Description = $"{WarDay} War Attendance [{WarDT.ToShortDateString()}]";
                if (attended.Count != 0)
                    builder.AddField("Attendance", string.Join(",\n", attended), true);
                if (missingName.Count != 0)
                    builder.AddField("Missing", string.Join(",\n", missingName), true);
              
                await UserExtensions.SendMessageAsync(guild.GetUser(Functions.Implementations.CREATOR_ID_FUNC()), embed: builder.Build());               
            }

            [Command("sample")]
            public async Task SampleWar()
            {
                // Work out what message we need to read signup from
                Regex nwRegex = new Regex(NODEWAR_REGEX);
                SocketGuild guild = CoreProgram.client.GetGuild(settings.GetData<ulong>(SERVER_ID_KEY));
                SocketTextChannel eventChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
                List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
                IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).Where(x => x.Embeds.Any(y => y.Title.Contains(WarUtility.WarDay.ToString(), StringComparison.InvariantCultureIgnoreCase))).First();
                IEmbed targetEmbed = selectedMessage.Embeds.First();
                EmbedField targetField = targetEmbed.Fields[2];

                List<string> SignupList = ExtractNamesFromEmbed(targetField.Value);

                // Get all signed up names and everyone in channel
                List<string> namesInChannels = ExtractWarChannelNames(guild);

                // What names are common between the attendance and signup
                List<string> commonNames = SignupList.Intersect(namesInChannels).ToList();
                // Who has attended but not signed up?
                List<string> attendedButNotSignedUp = namesInChannels.Except(SignupList).ToList();

                List<string> attended = Utilities.MatchNames(commonNames.Union(attendedButNotSignedUp), guild);
                List<string> missingName = Utilities.MatchNames(SignupList.Except(namesInChannels), guild);

                // Build an embed for the message
                EmbedBuilder builder = new EmbedBuilder();
                builder.Description = $"{WarUtility.WarDay} War Attendance [{WarUtility.WarDT.ToShortDateString()}]";
                if (attended.Count != 0)
                    builder.AddField("Attendance", string.Join(",\n", attended), true);
                if (missingName.Count != 0)
                    builder.AddField("Missing", string.Join(",\n", missingName), true);

                // Send attendance data to war discussion
                // SocketTextChannel wardiscChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARDISC_KEY));
                await UserExtensions.SendMessageAsync(guild.GetUser(Functions.Implementations.CREATOR_ID_FUNC()), embed: builder.Build());
            }

            [Command("test_next")]
            public async Task TestNextWar()
            {
                await ReplyAsync("Executing Test War...");
                WarUtility.OnBotStart();
                typeof(WarUtility).GetMethod("UntilWarTimer_Elapsed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this, null });
                WarUtility.IsTest = true;
                typeof(WarUtility).GetMethod("WarTimer_Elapsed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this, null });
            }

            // Print timer data in console
            [Command("timer_check")]
            public Task TimerCheck()
            {              
                TimerPlus untilNext = typeof(WarUtility).GetField("UntilWarTimer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as TimerPlus;
                TimerPlus warTimer = typeof(WarUtility).GetField("WarTimer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as TimerPlus;
                TimerPlus attendanceTimer = typeof(WarUtility).GetField("AttendanceTimer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as TimerPlus;

                double toNext = untilNext != null ? untilNext.TimeLeft : -1;
                double warNext = warTimer != null ? warTimer.TimeLeft : -1;
                double attend = attendanceTimer != null ? attendanceTimer.TimeLeft : -1;

                string untilNextFormatted = string.Format("{0:%d} day(s), {0:%h} hours, {0:%m} minutes", new TimeSpan(0, 0, 0, 0, (int)toNext));
                string warTimerFormatted = string.Format("{0:%h} hours, {0:%m} minutes, {0:%s} seconds", new TimeSpan(0, 0, 0, 0, (int)warNext));
                string attendanceFormatted = string.Format("{0:%s} seconds, {0:%f} ms", new TimeSpan(0, 0, 0, 0, (int)attend));

                string reply = $"Time Until Next War: {untilNextFormatted}\nTime Until War End: {warTimerFormatted}\nTime Until next Attendance Tick: {attendanceFormatted}\n";
                Console.WriteLine(reply);
                return Task.CompletedTask;
            }

            // Simple trial (idk what this does anymore)
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
                List<string> namesInChannels = ExtractWarChannelNames(Context.Guild);

                List<string> commonNames = signupNames.Intersect(namesInChannels).ToList();
                List<string> attendedButNotSignedUp = namesInChannels.Except(signupNames).ToList();

                List<(string, string)> allGuildMembers = GetAllGuildMembers(Context.Guild);

                List<string> attended = new List<string>();
                List<string> missingName = new List<string>();

                Console.WriteLine("Reading Attending Names...");
                foreach(string name in commonNames.Union(attendedButNotSignedUp))
                {
                    if (attended.Contains(name))
                        continue;

                    if(allGuildMembers.Any(x => x.Item1.Equals(name, StringComparison.InvariantCultureIgnoreCase) || x.Item2.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        attended.Add(name);
                    } else
                    {
                        List<string> fuzzy = Utilities.FuzzySearch(allGuildMembers.Select(x => x.Item1).Union(allGuildMembers.Select(x => x.Item2)), name);
                        Console.WriteLine($"Couldn't find name [{name}], substituting with [{fuzzy.First()}]");
                        attended.Add(fuzzy.First());
                    }
                }

                Console.WriteLine("Reading Missing Names...");
                foreach(string name in signupNames.Except(namesInChannels))
                {
                    if (missingName.Contains(name))
                        continue;

                    if(allGuildMembers.Any(x => x.Item1.Equals(name, StringComparison.InvariantCultureIgnoreCase) || x.Item2.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        missingName.Add(name);
                    } else
                    {
                        List<string> fuzzy = Utilities.FuzzySearch(allGuildMembers.Select(x => x.Item1).Union(allGuildMembers.Select(x => x.Item2)), name);
                        Console.WriteLine($"Couldn't find name [{name}], substituting with [{fuzzy.First()}]");
                        missingName.Add(fuzzy.First());
                    }
                }

                EmbedBuilder builder = new EmbedBuilder();
                builder.Description = "TRIAL WAR ATTENDANCE";
                if(attended.Count != 0)
                    builder.AddField("Attendance", string.Join(",\n", attended), true);
                if(missingName.Count != 0)
                    builder.AddField("Missing", string.Join(",\n", missingName), true);

                await ReplyAsync(message: $"Using signup from [{day}]", embed: builder.Build());
            }

            // Not sure
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

            // No idea
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
                    int index = 0;
                    foreach(EmbedField field in embeds.Fields)
                    {
                        Console.WriteLine($"Index: {index++}, Field Name: {field.Name}, Value: {field.Value}");
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();

                await ReplyAsync($"DEBUG FROM [{GetType().FullName} - {Utilities.GetMethod()}]");
            }

            // Eh?
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
