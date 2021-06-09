using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Discord;
using Oracle.ManagedDataAccess.Client;
using System.Data.Common;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using DismantledBot.DatabaseDataTypes;

namespace DismantledBot
{
    public class DatabaseManager
    {
        private OracleConnection MakeConnection()
        {
            OracleConnection connection = new OracleConnection();

            OracleConnectionStringBuilder ocsb = new OracleConnectionStringBuilder();
            ocsb.Password = CoreProgram.settings.DBPassword;
            ocsb.UserID = CoreProgram.settings.DBUserID;
            ocsb.DataSource = CoreProgram.settings.DataSource;
            ocsb.StatementCachePurge = true;
            string tnsAdmin = CoreProgram.settings.DBTNSAdminLocation;
            string oHome = Environment.GetEnvironmentVariable("ORACLE_HOME", EnvironmentVariableTarget.Machine);
            if (tnsAdmin.StartsWith("%ORACLE_HOME%"))
                tnsAdmin = tnsAdmin.Replace("%ORACLE_HOME%", oHome);
            ocsb.TnsAdmin = tnsAdmin;

            connection.ConnectionString = ocsb.ConnectionString;
            return connection;
        }

        /*public T Get<T>(Predicate<T> search)
        {
            DBTable table = Attribute.GetCustomAttribute(typeof(T), typeof(DBTable)) as DBTable;
            if (table == null)
                return default;

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"SELECT * FROM {table.TableName}", connection);
                OracleDataReader reader = query.ExecuteReader();

                while(reader.Read())
                {
                    T myT = (T)typeof(T).GetConstructor(new Type[] { }).Invoke(null);
                    foreach(FieldInfo field in typeof(T).GetFields())
                    {
                        string fieldName = field.GetCustomAttribute<DBField>().FieldName ?? field.Name;
                        if (reader.IsDBNull(fieldName))
                        {
                            field.SetValue(myT, null);
                            continue;
                        }

                        object val = reader[fieldName];
                        if(field.GetCustomAttribute<DBField>().FieldType == OracleDbType.Raw)
                        {
                            val = Utilities.TryBitConversion(val, field.FieldType);
                        } else if(!val.GetType().IsEquivalentTo(field.FieldType))
                        {
                            val = Convert.ChangeType(val, field.FieldType);
                        }
                        field.SetValue(myT, val);
                    }
                    if (search(myT))
                        return myT;
                }
                reader.Close();
            }

            return default;
        }

        public void Insert<T>(T obj)
        {
            DBTable table = Attribute.GetCustomAttribute(typeof(T), typeof(DBTable)) as DBTable;
            if (table == null)
                return;

            List<string> fields = typeof(T).GetFields().Select(x => x.GetCustomAttribute<DBField>().FieldName ?? x.Name).ToList();

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"INSERT INTO {table.TableName} ({string.Join(", ", fields)}) VALUES ({string.Join(", ", fields.Select(x => $":{x}"))}", connection);
                foreach(FieldInfo field in typeof(T).GetFields())
                {
                    string name = field.GetCustomAttribute<DBField>().FieldName ?? field.Name;
                    object val = field.GetValue(obj);
                    if (field.GetCustomAttribute<DBField>().FieldType == OracleDbType.Raw)
                        val = Utilities.ForwardBitConversion(val);
                    query.Parameters.Add(name, val);
                }
                query.ExecuteNonQuery();
            }
        }

        public void Remove<T>(T obj)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void Update1<T>(FieldInfo searchKey, object searchValue, params (FieldInfo field, object newValue)[] changes)
        {
            DBTable table = Attribute.GetCustomAttribute(typeof(T), typeof(DBTable)) as DBTable;
            if (table == null)
                return;

            List<string> fields = changes.Select(x => x.field.GetCustomAttribute<DBField>().FieldName ?? x.field.Name).ToList();
            string searchKeyName = searchKey.GetCustomAttribute<DBField>().FieldName ?? searchKey.Name;
            if (searchKey.GetCustomAttribute<DBField>().FieldType == OracleDbType.Raw)
                //searchValue = Utilities.ForwardBitConversion(searchValue);

            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"UPDATE {table.TableName} SET {string.Join(", ", fields.Select(x => $"{x} = :{x}"))} WHERE {searchKeyName} = :{searchKeyName}", connection);
                query.Parameters.Add(searchKeyName, searchValue);
                foreach((FieldInfo field, object newValue) in changes)
                {
                    string name = field.GetCustomAttribute<DBField>().FieldName ?? field.Name;
                    object val = newValue;
                    if (field.GetCustomAttribute<DBField>().FieldType == OracleDbType.Raw)
                        val = Utilities.ForwardBitConversion(val);
                    query.Parameters.Add(name, val);
                }              
                query.ExecuteNonQuery();
            }
        }*/

        public void RemoveUser(GuildMember user)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand removeCommand = new OracleCommand("DELETE FROM GuildMembers WHERE DiscordID = :DiscordID", connection);
                removeCommand.Parameters.AddValue(user, "RAWDiscordID");
                //removeCommand.Parameters.Add("DiscordID", user.DiscordID);
                removeCommand.ExecuteNonQuery();
            }
        }

        public void AddUser(GuildMember user)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand insertCommand = new OracleCommand("INSERT INTO GuildMembers (DiscordID, Username, Nickname) VALUES (:DiscordID, :Username, :Nickname)", connection);
                insertCommand.Parameters.AddValue(user, "RAWDiscordID");
                insertCommand.Parameters.AddValue(user, "Username");
                insertCommand.Parameters.AddValue(user, "Nickname");
                /*insertCommand.Parameters.Add("DiscordID", user.DiscordID);
                insertCommand.Parameters.Add("Username", user.Username);
                insertCommand.Parameters.Add("Nickname", Utilities.ForDB(user.Nickname));*/
                insertCommand.ExecuteNonQuery();
            }
        }

        public void UpdateUser(GuildMember user)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand updateCommand = new OracleCommand("UPDATE GuildMembers SET Nickname = :Nickname WHERE DiscordID = :DiscordID", connection);
                updateCommand.Parameters.AddValue(user, "Nickname");
                updateCommand.Parameters.AddValue(user, "RAWDiscordID");
                //updateCommand.Parameters.Add("Nickname", Utilities.ForDB(user.Nickname));
                //updateCommand.Parameters.Add("DiscordID", user.DiscordID);
                updateCommand.ExecuteNonQuery();
            }
        }

        public async Task ManageGuildMembers(List<IGuildUser> users)
        {           
            using(var connection = MakeConnection())
            {
                // Nickname change (update)
                List<GuildMember> updatedNames = new List<GuildMember>();
                // Joined server (insert)
                List<GuildMember> newNames = new List<GuildMember>();
                // Left/Changed username (delete)
                List<ulong> missingNames = new List<ulong>();       

                await connection.OpenAsync();
                OracleCommand getIds = new OracleCommand("SELECT DISCORDID, USERNAME, NICKNAME FROM GUILDMEMBERS", connection);
                OracleDataReader allIds = getIds.ExecuteReader();

                HashSet<GuildMember> members = new HashSet<GuildMember>(new GuildMember.Comparer());
                while (await allIds.ReadAsync())
                {
                    ulong id = allIds.Get<ulong>(0);
                    string username = allIds.GetString(1);
                    string nickname = allIds.IsDBNull(2) ? null : allIds.GetString(2);
                    members.Add(new GuildMember(id, username, nickname));
                }
                allIds.Close();

                List<GuildMember> modifedUsers = users.Select(x => new GuildMember(x)).ToList();                
                missingNames.AddRange(members.Where(x => !users.Any(y => string.Equals(x.Username, y.Username))).Select(x => x.DiscordID));
                updatedNames.AddRange(modifedUsers.Where(x =>
                {                   
                    if(!members.TryGetValue(x, out GuildMember value))
                    {
                        // Bad, but what can we do?
                        return false;
                    }

                    return !string.Equals(value.Nickname, x.Nickname);                  
                }));
                newNames.AddRange(modifedUsers.Where(x => !members.Contains(x)));

                if (missingNames.Count + updatedNames.Count + newNames.Count == 0)
                {
                    Console.WriteLine($"Completed guild member data update, no changed detected");
                    return;
                }

                OracleTransaction userTransaction = connection.BeginTransaction();
                OracleCommand removeCommand = new OracleCommand("DELETE FROM GuildMembers WHERE DiscordID = :DiscordID", connection);
                removeCommand.Transaction = userTransaction;
                OracleCommand updateCommand = new OracleCommand("UPDATE GuildMembers SET Nickname = :Nickname WHERE DiscordID = :DiscordID", connection);
                updateCommand.Transaction = userTransaction;
                OracleCommand insertCommand = new OracleCommand("INSERT INTO GUILDMEMBERS (DISCORDID, USERNAME, NICKNAME) VALUES (:DISCORDID, :USERNAME, :NICKNAME)", connection);
                insertCommand.Transaction = userTransaction;

                int deletedRowCount = 0;
                missingNames.ForEach(x =>
                {
                    removeCommand.Parameters.Clear();
                    removeCommand.Parameters.AddValue(x, "RAWDiscordID");
                    deletedRowCount += removeCommand.ExecuteNonQuery();
                });
                
                int updatedRowCount = 0;
                updatedNames.ForEach(x =>
                {
                    updateCommand.Parameters.Clear();
                    updateCommand.Parameters.AddValue(x, "Nickname");
                    updateCommand.Parameters.AddValue(x, "RAWDiscordID");
                    updatedRowCount += updateCommand.ExecuteNonQuery();
                });

                int additionRowCount = 0;
                newNames.ForEach(x =>
                {
                    insertCommand.Parameters.Clear();
                    insertCommand.Parameters.AddValue(x, "RAWDiscordID");
                    insertCommand.Parameters.AddValue(x, "Username");
                    insertCommand.Parameters.AddValue(x, "Nickname");
                    additionRowCount += insertCommand.ExecuteNonQuery();
                });

                userTransaction.Commit();

                Console.WriteLine($"Completed guild member data update: Removed {deletedRowCount} rows, Updated {updatedRowCount} rows, Added {additionRowCount} rows");
            };
        }
    }

    namespace DatabaseDataTypes
    {
        [AutoDBTable("GuildMembers")]
        public sealed class GuildMember
        {
            [AutoDBField(OracleDbType.Decimal, 8, FieldName = "DiscordID")]
            public decimal RAWDiscordID { get; private set; }

            [AutoDBField(OracleDbType.Varchar2, 64, FieldName = "Username")]
            public string Username { get; private set; }

            [AutoDBField(OracleDbType.Varchar2, 64, FieldName = "Nickname")]
            public string Nickname { get; private set; }

            [AutoDBIgnore]
            public ulong DiscordID { get => (ulong)Convert.ChangeType(RAWDiscordID, typeof(ulong)); }

            [AutoDBIgnore]
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
    }
}
