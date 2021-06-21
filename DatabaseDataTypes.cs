﻿using Discord;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DismantledBot
{
    [AutoDBTable("GuildMembers")]
    public sealed class GuildMember
    {
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "DiscordID")]
        public decimal RAWDiscordID;

        [AutoDBField(OracleDbType.Varchar2, 64, FieldName = "Username")]
        public string Username;

        [AutoDBField(OracleDbType.Varchar2, 64, FieldName = "Nickname")]
        public string Nickname;

        public ulong DiscordID { get => (ulong)Convert.ChangeType(RAWDiscordID, typeof(ulong)); }

        public string Name { get => Nickname ?? Username; }

        public GuildMember()
        {

        }

        public GuildMember(IGuildUser user)
        {
            RAWDiscordID = (decimal)Convert.ChangeType(user.Id, typeof(decimal));
            Username = user.Username;
            Nickname = user.Nickname;
        }

        public GuildMember(ulong id, string username, string nickname)
        {
            RAWDiscordID = (decimal)Convert.ChangeType(id, typeof(decimal));
            Username = username;
            Nickname = nickname;
        }

        public class Comparer : IEqualityComparer<GuildMember>
        {
            public bool Equals([AllowNull] GuildMember x, [AllowNull] GuildMember y)
            {
                return (x == null && y == null) || (x.DiscordID == y.DiscordID);
            }

            public int GetHashCode([DisallowNull] GuildMember obj)
            {
                return obj.DiscordID.GetHashCode();
            }
        }
    }

    [AutoDBTable("GuildTeams")]
    public sealed class GuildTeams
    {
        [AutoDBNoWrite]
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "TeamID")]
        public decimal TeamID { get; private set; }

        [AutoDBField(OracleDbType.Varchar2, 256, FieldName = "TeamName")]
        public string TeamName;

        [AutoDBIsForiegnKey(typeof(GuildMember), "DiscordID")]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "TeamLeader")]
        public decimal RAWTeamLeader;

        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "TeamRole")]
        public decimal TeamRole;

        public ulong TeamLeader { get => (ulong)Convert.ChangeType(RAWTeamLeader, typeof(ulong)); }

        public GuildTeams()
        {

        }

        public GuildTeams(string name, ulong leaderID, ulong role)
        {
            TeamName = name;
            RAWTeamLeader = (decimal)Convert.ChangeType(leaderID, typeof(decimal));
            TeamRole = (decimal)role;
        }

        public sealed class Comparer : IEqualityComparer<GuildTeams>
        {
            public bool Equals([AllowNull] GuildTeams x, [AllowNull] GuildTeams y)
            {
                return (x == null && y == null) || (x.TeamID == y.TeamID);
            }

            public int GetHashCode([DisallowNull] GuildTeams obj)
            {
                return obj.TeamID.GetHashCode();
            }
        }
    }

    [AutoDBTable("TeamMembers")]
    public sealed class TeamMember
    {
        [AutoDBIsPrimaryKey]
        [AutoDBMultiKey(OracleDbType.Decimal, "TeamID")]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "TeamID")]
        public decimal TeamID { get; private set; }
        private GuildTeams _Team = null;

        [AutoDBIsPrimaryKey]
        [AutoDBMultiKey(OracleDbType.Decimal, "DiscordID")]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "DiscordID")]
        public decimal DiscordID { get; private set; }
        private GuildMember _User = null;
        
        public TeamMember()
        {

        }

        public TeamMember(ulong teamID, ulong userID)
        {
            TeamID = teamID;
            DiscordID = userID;
        }

        public GuildTeams Team { get
            {
                if(_Team == null)
                {
                    _Team = CoreProgram.database.PerformSelection<GuildTeams>($"Select * From {typeof(GuildTeams).GetAutoTable().TableName} Where TeamID = {TeamID}").First();
                }

                return _Team;
            } 
        }

        public GuildMember User { get
            {
                if (_User == null)
                {
                    _User = CoreProgram.database.PerformSelection<GuildMember>($"Select * From {typeof(GuildMember).GetAutoTable().TableName} Where DiscordID = {DiscordID}").First();
                }

                return _User;
            }
        }

        public class Comparer : IEqualityComparer<TeamMember>
        {
            public bool Equals([AllowNull] TeamMember x, [AllowNull] TeamMember y)
            {
                return (x == null && y == null) || (x.DiscordID == y.DiscordID);
            }

            public int GetHashCode([DisallowNull] TeamMember obj)
            {
                return obj.DiscordID.GetHashCode();
            }
        }
    }

    [AutoDBTable("AdminRoles")]
    public sealed class AdminRoleInfo
    {
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "RoleID")]
        public decimal _RoleID { get; private set; }

        public ulong RoleID { get => (ulong)Convert.ChangeType(_RoleID, typeof(ulong)); }

        [AutoDBField(OracleDbType.Varchar2, 256, FieldName = "RoleName")]
        public string RoleName { get; private set; }

        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "RoleOrder")]
        public decimal RoleOrder { get; private set; }

        public AdminRoleInfo()
        {

        }

        public AdminRoleInfo(ulong id, string name, int order)
        {
            _RoleID = id;
            RoleName = name;
            RoleOrder = order;
        }

        public class Converter : IEqualityComparer<AdminRoleInfo>
        {
            public bool Equals([AllowNull] AdminRoleInfo x, [AllowNull] AdminRoleInfo y)
            {
                return (x == null && y == null) || (x.RoleID == y.RoleID);
            }

            public int GetHashCode([DisallowNull] AdminRoleInfo obj)
            {
                return obj.RoleID.GetHashCode();
            }
        }
    }

    [AutoDBTable("EventData")]
    public sealed class EventData
    {
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.TimeStamp, 8, FieldName = "EventDate")]
        public DateTime EventDate;

        [AutoDBField(OracleDbType.Varchar2, 256, FieldName = "EventName")]
        public string EventName;

        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "MessageID")]
        public decimal _MessageID;

        public ulong MessageID { get => (ulong)Convert.ChangeType(_MessageID, typeof(ulong)); }

        public EventData()
        {

        }

        public sealed class Comparer : IEqualityComparer<EventData>
        {
            public bool Equals([AllowNull] EventData x, [AllowNull] EventData y)
            {
                return (x == null && y == null) || DateTime.Equals(x.EventDate, y.EventDate);
            }

            public int GetHashCode([DisallowNull] EventData obj)
            {
                return obj.EventDate.GetHashCode();
            }
        }
    }

    [AutoDBTable("CurrentSignup")]
    public sealed class CurrentEventSignupData
    {
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "DiscordID")]
        public decimal DiscordID;

        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.TimeStamp, 8, FieldName = "EventDate")]
        public DateTime EventDate;

        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "SignupOrder")]
        public decimal SignupOrder;

        public sealed class Comparer : IEqualityComparer<CurrentEventSignupData>
        {
            public bool Equals([AllowNull] CurrentEventSignupData x, [AllowNull] CurrentEventSignupData y)
            {
                return (x == null && y == null) || (x.DiscordID == y.DiscordID && x.EventDate == y.EventDate);
            }

            public int GetHashCode([DisallowNull] CurrentEventSignupData obj)
            {
                return (obj.DiscordID, obj.EventDate).GetHashCode();
            }
        }
    }

    [AutoDBTable("PreviousEventData")]
    public sealed class PreviousEventData
    {
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.TimeStamp, 8, FieldName = "EventDate")]
        public DateTime EventDate;

        [AutoDBField(OracleDbType.Varchar2, 256, FieldName = "EventName")]
        public string EventName;

        public PreviousEventData()
        {

        }

        public PreviousEventData(EventData from)
        {
            EventDate = from.EventDate;
            EventName = from.EventName;
        }

        public sealed class Comparer : IEqualityComparer<PreviousEventData>
        {
            public bool Equals([AllowNull] PreviousEventData x, [AllowNull] PreviousEventData y)
            {
                return (x == null && y == null) || (x.EventDate == y.EventDate);
            }

            public int GetHashCode([DisallowNull] PreviousEventData obj)
            {
                return obj.EventDate.GetHashCode();
            }
        }
    }

    [AutoDBTable("PreviousSignupData")]
    public sealed class PreviousEventSignupData
    {
        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.Decimal, 8, FieldName = "DiscordID")]
        public decimal _DiscordID;

        [AutoDBIsPrimaryKey]
        [AutoDBField(OracleDbType.TimeStamp, 8, FieldName = "EventDate")]
        public DateTime EventDate;

        [AutoDBField(OracleDbType.Varchar2, 256, FieldName = "ChannelName")]
        public string ChannelName;

        public ulong DiscordID { get => (ulong)Convert.ChangeType(_DiscordID, typeof(ulong)); }

        public PreviousEventSignupData()
        {

        }

        public PreviousEventSignupData(CurrentEventSignupData data, string channel)
        {
            _DiscordID = data.DiscordID;
            EventDate = data.EventDate;
            ChannelName = channel;
        }
    }
}