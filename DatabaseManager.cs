using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Discord;
using Oracle.ManagedDataAccess.Client;
using System.Data.Common;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

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

        public void RemoveUser(DatabaseDataTypes.GuildMember user)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand removeCommand = new OracleCommand("DELETE FROM GuildMembers WHERE DiscordID = :DiscordID", connection);
                removeCommand.Parameters.Add("DiscordID", user.DiscordID);
                removeCommand.ExecuteNonQuery();
            }
        }

        public void AddUser(DatabaseDataTypes.GuildMember user)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand insertCommand = new OracleCommand("INSERT INTO GuildMembers (DiscordID, Username, Nickname) VALUES (:DiscordID, :Username, :Nickname)", connection);
                insertCommand.Parameters.Add("DiscordID", user.DiscordID);
                insertCommand.Parameters.Add("Username", user.Username);
                insertCommand.Parameters.Add("Nickname", Utilities.ForDB(user.Nickname));
                insertCommand.ExecuteNonQuery();
            }
        }

        public void UpdateUser(DatabaseDataTypes.GuildMember user)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand updateCommand = new OracleCommand("UPDATE GuildMembers SET Nickname = :Nickname WHERE DiscordID = :DiscordID", connection);
                updateCommand.Parameters.Add("Nickname", Utilities.ForDB(user.Nickname));
                updateCommand.Parameters.Add("DiscordID", user.DiscordID);
                updateCommand.ExecuteNonQuery();
            }
        }

        public async Task ManageGuildMembers(List<IGuildUser> users)
        {           
            using(var connection = MakeConnection())
            {
                // Nickname change (update)
                List<IGuildUser> updatedNames = new List<IGuildUser>();
                // Joined server (insert)
                List<IGuildUser> newNames = new List<IGuildUser>();
                // Left/Changed username (delete)
                List<ulong> missingNames = new List<ulong>();       

                await connection.OpenAsync();
                OracleCommand getIds = new OracleCommand("SELECT DISCORDID, USERNAME, NICKNAME FROM GUILDMEMBERS", connection);
                DbDataReader allIds = await getIds.ExecuteReaderAsync();

                Dictionary<ulong, string> nickData = new Dictionary<ulong, string>();
                Dictionary<ulong, string> usernameData = new Dictionary<ulong, string>();
                while (await allIds.ReadAsync())
                {
                    byte[] idBytes = (byte[])allIds[0];
                    ulong discordID = BitConverter.ToUInt64(idBytes);
                    nickData.Add(discordID, allIds.IsDBNull(2) ? string.Empty : allIds.GetString(2));
                    usernameData.Add(discordID, allIds.GetString(1));
                }
                allIds.Close();

                
                missingNames.AddRange(usernameData.Where(x => !users.Any(y => string.Equals(x.Value, y.Username))).Select(x => x.Key));
                updatedNames.AddRange(users.Where(x =>
                {
                    if(!nickData.TryGetValue(x.Id, out string nick))
                    {
                        // We have bigger problems, but a false is okay for now
                        return false;
                    }

                    return !string.Equals(nick, x.Nickname ?? "");
                }));
                newNames.AddRange(users.Where(x => !nickData.ContainsKey(x.Id)));

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
                OracleCommand insertCommand = new OracleCommand("INSERT INTO GuildMembers (DiscordID, Username, Nickname) VALUES (:DiscordID, :Username, :Nickname)", connection);
                insertCommand.Transaction = userTransaction;

                int deletedRowCount = 0;
                missingNames.ForEach(x =>
                {
                    removeCommand.Parameters.Clear();
                    removeCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(x));                    
                    deletedRowCount += removeCommand.ExecuteNonQuery();
                });
                
                int updatedRowCount = 0;
                updatedNames.ForEach(x =>
                {
                    updateCommand.Parameters.Clear();
                    updateCommand.Parameters.Add("Nickname", x.Nickname == null ? string.Empty : x.Nickname);
                    updateCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(x.Id));
                    updatedRowCount += updateCommand.ExecuteNonQuery();
                });

                int additionRowCount = 0;
                newNames.ForEach(x =>
                {
                    insertCommand.Parameters.Clear();
                    insertCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(x.Id));
                    insertCommand.Parameters.Add("Username", x.Username);
                    insertCommand.Parameters.Add("Nickname", x.Nickname == null ? string.Empty : x.Nickname);
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
        public sealed class GuildMember : AutoDBBase<GuildMember>
        {
            [AutoDBField(OracleDbType.Varchar2, 8, FieldName = "DiscordID")]
            public byte[] RAWDiscordID { get; private set; }

            [AutoDBField(OracleDbType.Varchar2, 64, FieldName = "Username")]
            public string Username { get; private set; }

            [AutoDBField(OracleDbType.Varchar2, 64, FieldName = "Nickname")]
            public string Nickname { get; private set; }

            [AutoDBIgnore]
            public ulong DiscordID { get => BitConverter.ToUInt64(RAWDiscordID); }

            [AutoDBIgnore]
            public string Name { get => Nickname ?? Username; }

            public GuildMember()
            {

            }

            public GuildMember(IGuildUser user)
            {
                RAWDiscordID = BitConverter.GetBytes(user.Id);
                Username = user.Username;
                Nickname = user.Nickname;
            }

            public override bool Equals([AllowNull] GuildMember x, [AllowNull] GuildMember y)
            {
                return (x == null && y == null) || (x.DiscordID == y.DiscordID);
            }

            public override int GetHashCode([DisallowNull] GuildMember obj)
            {
                return obj.DiscordID.GetHashCode();
            }
        }
    }
}
