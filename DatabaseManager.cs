﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Discord;
using Oracle.ManagedDataAccess.Client;
using System.Data.Common;
using System.Threading.Tasks;

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

        public void RemoveUser(IGuildUser user)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand removeCommand = new OracleCommand("DELETE FROM GuildMembers WHERE DiscordID = :DiscordID", connection);
                removeCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(user.Id));
                removeCommand.ExecuteNonQuery();
            }
        }

        public void AddUser(IGuildUser user)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand insertCommand = new OracleCommand("INSERT INTO GuildMembers (DiscordID, Username, Nickname) VALUES (:DiscordID, :Username, :Nickname)", connection);
                insertCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(user.Id));
                insertCommand.Parameters.Add("Username", user.Username);
                insertCommand.Parameters.Add("Nickname", user.Nickname ?? Convert.DBNull);
                insertCommand.ExecuteNonQuery();
            }
        }

        public void UpdateUser(IGuildUser user)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand updateCommand = new OracleCommand("UPDATE GuildMembers SET Nickname = :Nickname WHERE DiscordID = :DiscordID", connection);
                updateCommand.Parameters.Add("Nickname", user.Nickname ?? Convert.DBNull);
                updateCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(user.Id));
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
}
