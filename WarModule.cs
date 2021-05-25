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

namespace DismantledBot
{
    public static class WarUtility
    {
        public static bool IsRecordingWar { get; private set; } = false;

        private static TimerPlus UntilWarTimer;
        private static TimerPlus WarTimer;
        private static TimerPlus AttendanceTimer;

        private static List<string> Attendance;
        private static DayOfWeek WarDay;
        private static DateTime WarDT;
        private static int WarIndex;

        public static bool IsTest = false;

        public static void OnBotStart()
        {
            UntilWarTimer = null;
            WarTimer = null;
            AttendanceTimer = null;
            Attendance = null;
            WarIndex = -1;

            TimeSpan shortestSpan = TimeSpan.MaxValue;
            for(int i = 0; i < WarModule.settings.GetData<int>(WarModule.WAR_COUNT_KEY); i++)
            {
                int offset = WarModule.settings.GetData<int>($"{WarModule.SAVED_TIME_PREFIX}{i}");
                DateTime time = WarModule.settings.GetData<DateTime>($"{WarModule.SAVED_WAR_PREFIX}{i}").AddHours(offset);
                TimeSpan contender = time - DateTime.UtcNow;
                if (contender < shortestSpan)
                {
                    shortestSpan = contender;
                    WarDT = time.AddHours(-offset);
                    WarDay = WarDT.DayOfWeek;                    
                    WarIndex = i;
                }
            }

            shortestSpan = shortestSpan.Add(new TimeSpan(0, -30, 0));

            UntilWarTimer = new TimerPlus()
            {
                AutoReset = false,
                Interval = shortestSpan.TotalMilliseconds                
            };

            UntilWarTimer.Elapsed += UntilWarTimer_Elapsed;
            UntilWarTimer.Start();
        }

        private static async void UntilWarTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            UntilWarTimer.Stop();
            
            WarTimer = new TimerPlus()
            {
                AutoReset = false,
                Interval = new TimeSpan(2, 30, 0).TotalMilliseconds
            };

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

            SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
            SocketTextChannel wardisc = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARDISC_KEY));

            await wardisc.SendMessageAsync($"Now recording attendance for {WarDay} Nodewar [{WarDT.ToShortDateString()}]");
        }

        private static void AttendanceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Record who is currently in the channels
            if (Attendance == null)
                Attendance = new List<string>();

            SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
            List<string> names = WarModule.ExtractWarChannelNames(guild);

            Attendance.AddRange(names);
            Attendance = Attendance.Distinct().ToList();
        }

        private static async void WarTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Print war data
            IsRecordingWar = false;
            AttendanceTimer.Stop();
            WarTimer.Stop();
            AttendanceTimer_Elapsed(null, null);

            // Take attendance
            Regex nwRegex = new Regex(WarModule.NODEWAR_REGEX);
            SocketGuild guild = CoreProgram.client.GetGuild(WarModule.settings.GetData<ulong>(WarModule.SERVER_ID_KEY));
            SocketTextChannel eventChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_SIGNUP_KEY));
            List<IMessage> eventMessages = (await eventChannel.GetMessagesAsync().FlattenAsync()).ToList();
            IMessage selectedMessage = eventMessages.Where(x => nwRegex.Matches(x.Embeds.First().Description).Count != 0).Where(x => x.Embeds.First().Description.Contains(WarDay.ToString(), StringComparison.InvariantCultureIgnoreCase)).First();

            IEmbed targetEmbed = selectedMessage.Embeds.First();
            EmbedField targetField = targetEmbed.Fields[2];

            List<string> signupNames = WarModule.ExtractNamesFromEmbed(targetField.Value);
            List<string> namesInChannels = WarModule.ExtractWarChannelNames(guild);

            List<string> commonNames = signupNames.Intersect(namesInChannels).ToList();
            List<string> attendedButNotSignedUp = namesInChannels.Except(signupNames).ToList();

            List<(string, string)> allGuildMembers = WarModule.GetAllGuildMembers(guild);

            List<string> attended = new List<string>();
            List<string> missingName = new List<string>();

            Console.WriteLine("Reading Attending Names...");
            foreach (string name in commonNames.Union(attendedButNotSignedUp))
            {
                if (attended.Contains(name))
                    continue;

                if (allGuildMembers.Any(x => x.Item1.Equals(name, StringComparison.InvariantCultureIgnoreCase) || x.Item2.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    attended.Add(name);
                }
                else
                {
                    List<string> fuzzy = Utilities.FuzzySearch(allGuildMembers.Select(x => x.Item1).Union(allGuildMembers.Select(x => x.Item2)), name);
                    Console.WriteLine($"Couldn't find name [{name}], substituting with [{fuzzy.First()}]");
                    attended.Add(fuzzy.First());
                }
            }

            Console.WriteLine("Reading Missing Names...");
            foreach (string name in signupNames.Except(namesInChannels))
            {
                if (missingName.Contains(name))
                    continue;

                if (allGuildMembers.Any(x => x.Item1.Equals(name, StringComparison.InvariantCultureIgnoreCase) || x.Item2.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    missingName.Add(name);
                }
                else
                {
                    List<string> fuzzy = Utilities.FuzzySearch(allGuildMembers.Select(x => x.Item1).Union(allGuildMembers.Select(x => x.Item2)), name);
                    Console.WriteLine($"Couldn't find name [{name}], substituting with [{fuzzy.First()}]");
                    missingName.Add(fuzzy.First());
                }
            }

            EmbedBuilder builder = new EmbedBuilder();
            builder.Description = $"{WarDay} War Attendance [{WarDT.ToShortDateString()}]";
            if (attended.Count != 0)
                builder.AddField("Attendance", string.Join(",\n", attended), true);
            if (missingName.Count != 0)
                builder.AddField("Missing", string.Join(",\n", missingName), true);

            SocketTextChannel wardiscChannel = guild.GetTextChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARDISC_KEY));
            await wardiscChannel.SendMessageAsync(embed: builder.Build());

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
        public const string TIME_EXTRACT_REGEX = @"(?<DAY>[A-Za-z]*) (?<MONTH>[A-Za-z]*) (?<DNUM>[0-9]*)(th|st|nd|rd), (?<YEAR>[0-9]*) .* (?<HOUR>[0-9]*):(?<MINUTE>[0-9]*) (?<ZONE>[A-Z]*) \((?<REL>[A-Z]*)(?<OFSS>\+|-)(?<OFSN>[0-9]*)\)";
        public const string NODEWAR_REGEX = @"(n|N)(o|O)(d|D)(e|E) (w|W)(a|A)(r|R)";

        public const string SAVED_WAR_PREFIX = "WAR_";
        public const string SAVED_TIME_PREFIX = "WTO_";
        public const string WAR_COUNT_KEY = "WCK";
        public const string SERVER_ID_KEY = "SIDK";

        public static ModuleSettingsManager<WarModule> settings = ModuleSettingsManager<WarModule>.MakeSettings();

        public static List<string> ExtractNamesFromEmbed(string input)
        {
            input = input.Substring(4);
            string[] splits = input.Split('\n');

            return splits.Select(x => x.Replace("\\", "")).ToList();
        }

        public static List<string> ExtractWarChannelNames(SocketGuild guild)
        {
            List<SocketVoiceChannel> warChannels = guild.GetCategoryChannel(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_WARCAT_KEY)).Channels.Where(x => x is SocketVoiceChannel).Select(x => x as SocketVoiceChannel).ToList();

            List<SocketGuildUser> warUsers = warChannels.SelectMany(x => x.Users).ToList();
            return warUsers.Select(x => string.IsNullOrEmpty(x.Nickname) ? x.Username : x.Nickname).ToList();
        }

        public static List<(string, string)> GetAllGuildMembers(SocketGuild guild)
        {
            SocketRole selectedRole = guild.GetRole(BindingModule.settings.GetData<ulong>(BindingModule.BINDING_GMEMBER_KEY));
            return guild.Users.Where(x => x.Roles.Contains(selectedRole)).Select(x => (string.IsNullOrEmpty(x.Nickname) ? "" : x.Nickname, x.Username)).ToList();
        }

        [Command("force_start")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task ForceWarStart()
        {
            await ReplyAsync("Forcing war start...");
            WarUtility.OnBotStart();          
            typeof(WarUtility).GetMethod("UntilWarTimer_Elapsed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this, null });
        }

        [Command("force_end")]
        [HasRole(Functions.Names.OFFICER_ROLE_FUNC)]
        public async Task ForceWarEnd(bool isTest = false)
        {
            await ReplyAsync("Forcing war end...");
            if (isTest)
                WarUtility.IsTest = true;
            typeof(WarUtility).GetMethod("WarTimer_Elapsed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this, null });            
        }

        [Command("calibrate", RunMode = RunMode.Async)]
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
                if (nwRegex.Matches(embed.Description).Count == 0 || embed.Fields.Length == 0)
                    continue;

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
                int dmonth = DateTime.ParseExact(month, "MMMM", CultureInfo.CurrentCulture).Month;
                int dday = int.Parse(dayNumber);
                int dhour = int.Parse(hour);
                int dmin = int.Parse(minutes);

                DateTime nwTime = new DateTime(dyear, dmonth, dday, dhour, dmin, 0, DateTimeKind.Utc);
                TimeSpan timeUntil = nwTime - DateTime.UtcNow;
                while(timeUntil.Ticks <= 0)
                {
                    nwTime = nwTime.AddDays(7);
                    timeUntil = DateTime.UtcNow - nwTime;
                }

                int warOffsetTime = int.Parse(offsetNumber);
                if (offsetSign.Equals("+"))
                    warOffsetTime *= -1;

                settings.SetData($"{SAVED_TIME_PREFIX}{warNum}", warOffsetTime);
                settings.SetData($"{SAVED_WAR_PREFIX}{warNum++}", nwTime);         
            }

            settings.SetData(WAR_COUNT_KEY, warNum);
            settings.SetData(SERVER_ID_KEY, Context.Guild.Id);
            WarUtility.OnBotStart();
            await ReplyAsync($"Calibrated for [{warNum}] Nodewar(s)");
        }
        
        [Command("next")]
        public async Task FindNextWar()
        {
            TimerPlus untilNext = typeof(WarUtility).GetField("UntilWarTimer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as TimerPlus;
            double toNext = untilNext != null ? untilNext.TimeLeft : -1;
            string untilNextFormatted = string.Format("{0:%d} day(s), {0:%h} hours, {0:%m} minutes", new TimeSpan(0, 0, 0, 0, (int)toNext));
            await ReplyAsync($"Time until next War: {untilNextFormatted}");
        }

        [Group("debug")]
        [IsCreator]
        public class DebugModule : ModuleBase<SocketCommandContext>
        {
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

                string reply = $"Time Until Next War: {untilNextFormatted}\nTime Until War End: {warTimerFormatted}\nTime Until next Attendance Tick: {attendanceFormatted}";
                Console.WriteLine(reply);
                return Task.CompletedTask;
            }

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
