using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Discord;
using Oracle.ManagedDataAccess.Client;
using System.Threading.Tasks;
using System.Reflection;

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
        #region Auto Segment 1
        public int InsertSingleExpressive<T>(T obj)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            var allFieldData = Utilities.GetAllAutoDBFieldInformation<T>();
            var allWriteFields = allFieldData.Where(x => !x.IsNoWrite);
            List<AutoDBFieldInformation> allMappedWriteFields = new List<AutoDBFieldInformation>();
            List<AutoDBFieldInformation> multiKeyFields = new List<AutoDBFieldInformation>();
            List<string> queryFields = new List<string>();
            // TODO: Work out how to include other objects as foreingn keys?
            foreach(AutoDBFieldInformation fInfo in allWriteFields)
            {
                // If the value is not a foreign key, or is, but is primitive, write it and continue
                if(!fInfo.IsForiegnKey || fInfo.IsContainingFieldPrimitive)
                {
                    allMappedWriteFields.Add(fInfo);
                    continue;
                }                
            }
            queryFields.AddRange(multiKeyFields.Select(x => x.FieldData.FieldName));
            queryFields.AddRange(allMappedWriteFields.Select(x => x.FieldData.FieldName));
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"INSERT INTO {table.TableName} ({string.Join(", ", queryFields)}) VALUES ({string.Join(", ", queryFields.Select(x => $":{x}"))})", connection);
                foreach(AutoDBFieldInformation fInfo in allMappedWriteFields)
                {
                    query.Parameters.AddValue(obj, fInfo.FieldData.FieldName);
                }
                return query.ExecuteNonQuery();
            }
        }
        #endregion

        #region Auto Segment 0
        public int DeleteMultiple<T>(HashSet<T> objects, params string[] selectionFields)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            var allFields = Utilities.GetAllAutoFieldsWithNoWrite<T>();
            var scFields = allFields.Where(x => selectionFields != null && selectionFields.Contains(x.FieldName)).ToList();
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"DELETE FROM {table.TableName} WHERE {string.Join(", ", scFields.Select(x => $"{x.FieldName} = :{x.FieldName}"))}", connection);
                OracleTransaction transaction = connection.BeginTransaction();
                query.Transaction = transaction;
                int deletions = 0;
                foreach(T obj in objects)
                {
                    query.Parameters.Clear();
                    foreach (AutoDBField selection in scFields)
                    {
                        query.Parameters.AddValue(obj, selection.FieldName);
                    }
                    deletions += query.ExecuteNonQuery();
                }
                transaction.Commit();
                return deletions;
            }
        }

        public int DeleteSingle<T>(T obj, params string[] selectionFields)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            var allFields = Utilities.GetAllAutoFieldsWithNoWrite<T>();
            var scFields = allFields.Where(x => selectionFields != null && selectionFields.Contains(x.FieldName)).ToList();
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"DELETE FROM {table.TableName} WHERE {string.Join(", ", scFields.Select(x => $"{x.FieldName} = :{x.FieldName}"))}", connection);
                foreach(AutoDBField selection in scFields)
                {
                    query.Parameters.AddValue(obj, selection.FieldName);
                }
                return query.ExecuteNonQuery();
            }
        }

        public int ModifyMultiple<T>(HashSet<T> objects, string[] updatedFields, string[] selectionFields)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            var allFields = Utilities.GetAllAutoFieldsWithNoWrite<T>();
            var upFields = allFields.Where(x => updatedFields != null && updatedFields.Contains(x.FieldName)).ToList();
            var scFields = allFields.Where(x => selectionFields != null && selectionFields.Contains(x.FieldName)).ToList();
            if (Utilities.ContainsNoWriteField<T>(upFields.Select(x => x.FieldName)))
                return 0;
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"UPDATE {table.TableName} SET {string.Join(", ", upFields.Select(x => $"{x.FieldName} = :{x.FieldName}"))} WHERE {string.Join(", ", scFields.Select(x => $"{x.FieldName} = :{x.FieldName}"))}", connection);
                OracleTransaction transaction = connection.BeginTransaction();
                query.Transaction = transaction;
                int modified = 0;
                foreach(T obj in objects)
                {
                    query.Parameters.Clear();
                    foreach (AutoDBField update in upFields)
                    {
                        query.Parameters.AddValue(obj, update.FieldName);
                    }
                    foreach (AutoDBField selection in scFields)
                    {
                        query.Parameters.AddValue(obj, selection.FieldName);
                    }
                    modified += query.ExecuteNonQuery();
                }
                transaction.Commit();
                return modified;
            }
        }

        public int ModifySingle<T>(T obj, string[] updatedFields, string[] selectionFields)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            var allFields = Utilities.GetAllAutoFieldsWithNoWrite<T>();
            var upFields = allFields.Where(x => updatedFields != null && updatedFields.Contains(x.FieldName)).ToList();
            var scFields = allFields.Where(x => selectionFields != null && selectionFields.Contains(x.FieldName)).ToList();
            if (Utilities.ContainsNoWriteField<T>(upFields.Select(x => x.FieldName)))
                return 0;
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"UPDATE {table.TableName} SET {string.Join(", ", upFields.Select(x => $"{x.FieldName} = :{x.FieldName}"))} WHERE {string.Join(", ", scFields.Select(x => $"{x.FieldName} = :{x.FieldName}"))}", connection);
                foreach(AutoDBField update in upFields)
                {
                    query.Parameters.AddValue(obj, update.FieldName);
                }
                foreach(AutoDBField selection in scFields)
                {
                    query.Parameters.AddValue(obj, selection.FieldName);
                }
                return query.ExecuteNonQuery();
            }
        }

        public int Insert<T>(HashSet<T> objects)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            using (var connection = MakeConnection())
            {
                connection.Open();
                var fields = Utilities.GetAllAutoFields<T>();
                OracleCommand query = new OracleCommand($"INSERT INTO {table.TableName} ({string.Join(", ", fields.Select(x => x.FieldName))}) VALUES ({string.Join(", ", fields.Select(x => $":{x.FieldName}"))})", connection);
                OracleTransaction transaction = connection.BeginTransaction();
                query.Transaction = transaction;
                int rowsModified = 0;
                foreach(T obj in objects)
                {
                    query.Parameters.Clear();
                    foreach (AutoDBField f in fields)
                    {
                        query.Parameters.AddValue(obj, f.FieldName);
                    }
                    rowsModified += query.ExecuteNonQuery();
                }
                transaction.Commit();
                return rowsModified;
            }
        }

        public int InsertSingle<T>(T obj)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return 0;
            using(var connection = MakeConnection())
            {
                connection.Open();
                var fields = Utilities.GetAllAutoFields<T>();
                OracleCommand query = new OracleCommand($"INSERT INTO {table.TableName} ({string.Join(", ", fields.Select(x => x.FieldName))}) VALUES ({string.Join(", ", fields.Select(x => $":{x.FieldName}"))})", connection);
                foreach(AutoDBField f in fields)
                {
                    query.Parameters.AddValue(obj, f.FieldName);
                }
                return query.ExecuteNonQuery();
            }
        }

        public HashSet<T> GetRows<T>(IEqualityComparer<T> comparer)
        {
            AutoDBTable table = typeof(T).GetAutoTable();
            if (table == null)
                return null;

            HashSet<T> allRows = new HashSet<T>(comparer);
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand($"Select * From {table.TableName}", connection);
                using (OracleDataReader reader = query.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        T myObj = (T)typeof(T).GetConstructor(new Type[] { }).Invoke(null);
                        foreach (PropertyInfo property in typeof(T).GetProperties())
                        {
                            AutoDBField autoField = property.GetAutoField();
                            if (autoField == null)
                                continue;
                            string name = autoField.FieldName ?? property.Name;
                            if (reader.IsDBNull(name))
                            {
                                property.SetValue(myObj, null);
                                continue;
                            }

                            object val = reader[name];
                            val = Convert.ChangeType(val, property.PropertyType);
                            property.SetValue(myObj, val);
                        }
                        foreach (FieldInfo field in typeof(T).GetFields())
                        {
                            AutoDBField autoField = field.GetAutoField();
                            if (autoField == null)
                                continue;
                            string name = autoField.FieldName ?? field.Name;
                            if (reader.IsDBNull(name))
                            {
                                field.SetValue(myObj, null);
                                continue;
                            }

                            object val = reader[name];
                            val = Convert.ChangeType(val, field.FieldType);
                            field.SetValue(myObj, val);
                        }
                        allRows.Add(myObj);
                    }
                }                                
            }
            return allRows;
        }

        public HashSet<T> GetRows<T>(IEqualityComparer<T> comparer, Predicate<T> search)
        {
            HashSet<T> rSet = GetRows<T>(comparer);
            if (rSet == null)
                return null;
            return rSet.Where(x => search(x)).ToHashSet();
        }

        #endregion
        public void RemoveUser(GuildMember user)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand removeCommand = new OracleCommand("DELETE FROM GuildMembers WHERE DiscordID = :DiscordID", connection);
                removeCommand.Parameters.AddValue(user, "RAWDiscordID");
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
                List<GuildMember> missingNames = new List<GuildMember>();       

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
                missingNames.AddRange(members.Where(x => !users.Any(y => string.Equals(x.Username, y.Username))));
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
}
