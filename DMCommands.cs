using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using NodaTime;

namespace DismantledBot
{
    [RequireContext(ContextType.DM)]
    public sealed class DMCommands : ModuleBase<SocketCommandContext>
    {
        [Command("whoami")]
        public async Task RunWhoAmI()
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketGuildUser user = guild.GetUser(Context.User.Id);

            HashSet<AdminRoleInfo> adminRoles = CoreProgram.database.GetRows(new AdminRoleInfo.Converter());
            var userRoles = user.Roles.Select(x => x.Id);
            var userAdminRoles = adminRoles.Where(x => userRoles.Contains(x.RoleID)).OrderByDescending(x => x.RoleOrder);
            bool isAdmin = user.Id == Functions.Implementations.GET_ADMIN_FUNC();
            string adminRoleResponse = "You have no administrative roles on this server";
            if(userAdminRoles.Count() != 0)
            {
                adminRoleResponse = $"You have the following administrative roles:\n{string.Join(",\n", userAdminRoles.Select(x => $"{x.RoleName} / {x.RoleID}"))}";
            }
            await ReplyAsync($"Who Am I?\nI Am bound to [{guild.Name}], ID = {guild.Id}\nYou are [{user.Nickname ?? user.Username}]\nYou are{(isAdmin ? string.Empty : " not")} the bots administrator\n{adminRoleResponse}");
        }

        [HasRole(Functions.Names.GET_GUILD_MEMBER_FUNC)]
        public sealed class PrivlegedL1Commands : ModuleBase<SocketCommandContext>
        {

        }

        [IsUser(Functions.Names.GET_OWNER_FUNC, Functions.Names.GET_ADMIN_FUNC)]
        public sealed class PrivilegedL2Commands : ModuleBase<SocketCommandContext>
        {
            [Command("sync teams")]
            public async Task SynchTeams()
            {
                HashSet<GuildTeams> teams = CoreProgram.database.GetRows(new GuildTeams.Comparer());
                HashSet<TeamMember> teamMembers = new HashSet<TeamMember>(new TeamMember.Comparer());
                var allGuildMembers = await CoreProgram.BoundGuild.GetUsersAsync().FlattenAsync();
                foreach(GuildTeams team in teams)
                {
                    var allTeamMembers = allGuildMembers.Where(x => x.RoleIds.Contains((ulong)team.TeamRole));
                    allTeamMembers.ToList().ForEach(x => teamMembers.Add(new TeamMember((ulong)team.TeamID, x.Id)));
                }
                CoreProgram.database.Insert(teamMembers);
                await ReplyAsync("Teams successfully synchronised!");
            }

            [Command("add team")]
            public async Task AddTeam(string name, ulong teamLeader, ulong teamRole)
            {
                GuildTeams team = new GuildTeams(name, teamLeader, teamRole);
                CoreProgram.database.InsertSingle(team);
                await ReplyAsync($"[{team.TeamName}] lead by [{CoreProgram.BoundGuild.GetUser(team.TeamLeader)}] registered!");
            }

            [Command("list teams")]
            public async Task ListTeams()
            {
                HashSet<GuildTeams> teams = CoreProgram.database.GetRows(new GuildTeams.Comparer());
                string teamReply = "No teams!";
                if (teams.Count != 0)
                    teamReply = string.Join(",\n", teams.Select(x => $"[{x.TeamName}] lead by [{CoreProgram.BoundGuild.GetUser(x.TeamLeader)}]"));
                await ReplyAsync($"Teams List:\n{teamReply}");
            }

            #region Admin Role Commands
            [Command("add admin role")]
            public async Task AddAdminRoleByID(ulong id)
            {
                SocketGuild guild = CoreProgram.BoundGuild;
                SocketRole foundRole = guild.GetRole(id);
                if (foundRole == null)
                {
                    await ReplyAsync("Could not find role with selected ID in bound server!");
                    return;
                }

                AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
                CoreProgram.database.InsertSingle(roleInfo);
                await ReplyAsync("Added role to administrative role list!");
            }

            [Command("add admin role")]
            public async Task AddAdminRoleByName(string name)
            {
                SocketGuild guild = CoreProgram.BoundGuild;
                SocketRole foundRole = guild.Roles.ToList().Find(x => string.Equals(name, x.Name));
                if (foundRole == null)
                {
                    await ReplyAsync("Could not find role with the given name in bound server!");
                    return;
                }

                AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
                CoreProgram.database.InsertSingle(roleInfo);
                await ReplyAsync("Added role to administrative role list!");
            }

            [Command("remove admin role")]
            public async Task RemoveAdminRoleByID(ulong id)
            {
                SocketGuild guild = CoreProgram.BoundGuild;
                SocketRole foundRole = guild.GetRole(id);
                if (foundRole == null)
                {
                    await ReplyAsync("Could not find role with selected ID in bound server!");
                    return;
                }

                AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
                CoreProgram.database.DeleteSingle(roleInfo, "RoleID");
                await ReplyAsync("Successfully removed role from administrative list!");
            }

            [Command("remove admin role")]
            public async Task RemoveAdminRoleByName(string name)
            {
                SocketGuild guild = CoreProgram.BoundGuild;
                SocketRole foundRole = guild.Roles.ToList().Find(x => string.Equals(name, x.Name));
                if (foundRole == null)
                {
                    await ReplyAsync("Could not find role with selected ID in bound server!");
                    return;
                }

                AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
                CoreProgram.database.DeleteSingle(roleInfo, "RoleID");
                await ReplyAsync("Successfully removed role from administrative list!");
            }
            #endregion
        }

        [IsBotAdmin]
        public sealed class PrivilegedL3Commands : ModuleBase<SocketCommandContext>
        {            
            [Command("debug signup")]
            public async Task DebugSignup(ulong messageId, ulong categoryID)
            {
                HashSet<EventData> selectedEvent = CoreProgram.database.GetRows(new EventData.Comparer(), x => x.MessageID == messageId);
                EventData target = selectedEvent.Count == 0 ? null : selectedEvent.First();
                if(target == null)
                {
                    await ReplyAsync("Cannot find target event!");
                    return;
                }

                SocketCategoryChannel warCat = CoreProgram.BoundGuild.GetCategoryChannel(categoryID);
                var voiceChannels = warCat.Channels.Select(x => x as SocketVoiceChannel).Where(x => x != null);

                HashSet<ulong> inChannel = voiceChannels.SelectMany(x => x.Users).Where(x => x != null).Select(x => x.Id).ToHashSet();
                HashSet<GuildMember> attendedMembers = CoreProgram.database.GetRows(new GuildMember.Comparer(), x => inChannel.Contains(x.DiscordID));
                HashSet<GuildTeams> teams = CoreProgram.database.GetRows(new GuildTeams.Comparer());
                HashSet<TeamMember> teamData = CoreProgram.database.GetRows(new TeamMember.Comparer());

                Dictionary<GuildTeams, List<GuildMember>> attendanceData = new Dictionary<GuildTeams, List<GuildMember>>();
                foreach (GuildMember user in attendedMembers)
                {
                    try
                    {
                        GuildTeams userTeam = teams.Find(x => x.TeamID == teamData.Find(x => x.DiscordID == user.DiscordID).TeamID);
                        List<GuildMember> teamList = attendanceData.GetValueOrDefault(userTeam, new List<GuildMember>());
                        teamList.Add(user);
                        attendanceData[userTeam] = teamList;
                    }
                    catch (Exception ex)
                    {
                        CoreProgram.logger.Write2(Logger.MAJOR, ex.Message);
                        continue;
                    }
                }

                EmbedBuilder builder = new EmbedBuilder();
                builder.Title = "Nodewar Attendance";
                DateTime warDT = target.EventDate;
                ZonedDateTime zdt = new ZonedDateTime(Instant.FromUtc(warDT.Year, warDT.Month, warDT.Day, warDT.Hour, warDT.Minute), DateTimeZone.Utc);
                DateTime realDT = Utilities.ConvertDateTimeToDifferentTimeZone(zdt.LocalDateTime, zdt.Zone.Id, DateTimeZoneProviders.Tzdb["CST6CDT"].Id).ToDateTimeUnspecified();
                builder.Description = $"{realDT.DayOfWeek}, {realDT.ToShortDateString()}";
                string attendanceReply = "-";
                string teamAttendanceReply = "-";
                if (attendanceData.Count != 0)
                    teamAttendanceReply = string.Join("\n", attendanceData.Select(x => $"{x.Key.TeamName}: {x.Value.Count}"));
                if (attendedMembers.Count != 0)
                    attendanceReply = string.Join("\n", attendedMembers.Select(x => x.Name));
                builder.AddField("Team Attendance", teamAttendanceReply);
                builder.AddField("Attendance", attendanceReply);

                await ReplyAsync("This is a debug message!", embed: builder.Build());
            }

            private static List<GuildMember> lastSelection = null;
            private static DateTime lastTime;

            [Command("add event user")]
            public async Task OverrideUserAdd(DateTime eventDate, string fuzzyName)
            {
                HashSet<EventData> events = CoreProgram.database.GetRows(new EventData.Comparer(), x => x.EventDate == eventDate);
                EventData targetEvent = events.Count == 0 ? null : events.First();
                if(targetEvent == null)
                {
                    await ReplyAsync("Could not locate event with given date!");
                    return;
                }

                HashSet<GuildMember> foundMembers = CoreProgram.database.PerformSelection<GuildMember>($"Select * From {typeof(GuildMember).GetAutoTable().TableName} Where Username LIKE '%{fuzzyName}%' OR Nickname LIKE '%{fuzzyName}%'");
                if(foundMembers == null || foundMembers.Count == 0)
                {
                    await ReplyAsync($"Could not locate user with name like [{fuzzyName}]!");
                    return;
                }

                if(foundMembers.Count == 1)
                {
                    CurrentEventSignupData data = new CurrentEventSignupData()
                    {
                        DiscordID = foundMembers.First().DiscordID,
                        EventDate = targetEvent.EventDate
                    };
                    CoreProgram.database.InsertSingle(data);
                    await ReplyAsync($"Successfully inserted override for [{foundMembers.First().Name}]!");
                    return;
                }

                lastSelection = foundMembers.ToList();
                lastTime = targetEvent.EventDate;
                await ReplyAsync($"Multiple members found with matching names, please enter the select index command for the one to select, or 'select -1' to cancel:\n{string.Join(",\n", lastSelection.Select(x => $"{lastSelection.IndexOf(x)} -> {x.Name}"))}");
            }

            [Command("select")]
            public async Task SelectOverride(int value)
            {
                if(lastSelection == null)
                {
                    await ReplyAsync("Cannot select override at this time!");
                    return;
                }

                if(value == -1 || value >= lastSelection.Count)
                {
                    lastSelection = null;
                    await ReplyAsync("Selection canceled!");
                    return;
                }

                GuildMember ovr = lastSelection[value];
                CurrentEventSignupData data = new CurrentEventSignupData()
                {
                    DiscordID = ovr.DiscordID,
                    EventDate = lastTime
                };
                CoreProgram.database.InsertSingle(data);
                lastSelection = null;
                await ReplyAsync("Override inserted!");
            }
        }       
    }
}
