using Discord;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

        public ulong TeamLeader { get => (ulong)Convert.ChangeType(RAWTeamLeader, typeof(ulong)); }

        public GuildTeams()
        {

        }

        public GuildTeams(string name, ulong leaderID)
        {
            TeamName = name;
            RAWTeamLeader = (decimal)Convert.ChangeType(leaderID, typeof(decimal));
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
        [AutoDBIsForiegnKey(typeof(GuildTeams), "TeamID")]
        [AutoDBMultiKey(OracleDbType.Decimal, "TeamID")]
        public GuildTeams Team { get; private set; }

        [AutoDBIsPrimaryKey]
        [AutoDBIsForiegnKey(typeof(GuildMember), "DiscordID")]
        [AutoDBMultiKey(OracleDbType.Decimal, "DiscordID")]
        public GuildMember GuildMember { get; private set; }

        public decimal TeamID { get => Team.TeamID; }
        public ulong DiscordID { get => GuildMember.DiscordID; } 
    }
}
