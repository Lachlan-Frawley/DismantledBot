using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data.Odbc;
using System.Data;
using Discord;

namespace DismantledBot
{
    public class DatabaseManager
    {
        private readonly string ConnectionString;

        public DatabaseManager(string databasePath)
        {
            ConnectionString = @"Driver={MICROSOFT ACCESS DRIVER (*.mdb, *.accdb)};DBQ=" + databasePath;            

            if (!File.Exists(databasePath))
            {
                throw new FileNotFoundException("Cannot find database! (Did you forget to move the blank copy to the appropriate directory?)");
            }         
        }

        public void ManageGuildMembers(List<IGuildUser> users)
        {
            using(var connection = new OdbcConnection(ConnectionString))
            {
                // Nickname change (update)
                List<IGuildUser> updatedNames = new List<IGuildUser>();
                // Joined server (insert)
                List<IGuildUser> newNames = new List<IGuildUser>();
                // Left/Changed username (delete)
                List<ulong> missingNames = new List<ulong>();

                connection.Open();
                OdbcCommand getIds = new OdbcCommand("SELECT DiscordID, Username, Nickname FROM GuildMembers;", connection);
                OdbcDataReader allIds = getIds.ExecuteReader();

                Dictionary<ulong, string> nickData = new Dictionary<ulong, string>();
                Dictionary<ulong, string> usernameData = new Dictionary<ulong, string>();
                while (allIds.Read())
                {
                    nickData.Add(allIds.Get<ulong>(0), allIds.GetString(2));
                    usernameData.Add(allIds.Get<ulong>(0), allIds.GetString(1));
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
                    Console.WriteLine($"Completed guild member data update, data unchanged");
                    return;
                }

                OdbcTransaction userTransaction = connection.BeginTransaction();
                OdbcCommand removeCommand = new OdbcCommand("DELETE FROM GuildMembers WHERE DiscordID = ?;", connection, userTransaction);
                OdbcCommand updateCommand = new OdbcCommand("UPDATE GuildMembers SET Nickname = ? WHERE DiscordID = ?;", connection, userTransaction);
                OdbcCommand insertCommand = new OdbcCommand("INSERT INTO GuildMembers (DiscordID, Username, Nickname)\nVALUES (?, ?, ?);", connection, userTransaction);

                int deletedRowCount = 0;
                missingNames.ForEach(x =>
                {
                    removeCommand.Parameters.Clear();
                    removeCommand.Parameters.AddWithValue("@DiscordID", x);
                    deletedRowCount += removeCommand.ExecuteNonQuery();
                });

                int updatedRowCount = 0;
                updatedNames.ForEach(x =>
                {
                    updateCommand.Parameters.Clear();
                    updateCommand.Parameters.AddWithValue("@Nickname", x.Nickname == null ? string.Empty : x.Nickname);
                    updateCommand.Parameters.AddWithValue("@DiscordID", Convert.ToDecimal(x.Id));
                    updatedRowCount += updateCommand.ExecuteNonQuery();
                });

                int additionRowCount = 0;
                newNames.ForEach(x =>
                {
                    insertCommand.Parameters.Clear();
                    insertCommand.Parameters.AddWithValue("@DiscordID", Convert.ToDecimal(x.Id));
                    insertCommand.Parameters.AddWithValue("@Username", x.Username);
                    insertCommand.Parameters.AddWithValue("@Nickname", x.Nickname == null ? string.Empty : x.Nickname);
                    additionRowCount += insertCommand.ExecuteNonQuery();
                });

                userTransaction.Commit();

                Console.WriteLine($"Completed guild member data update: Removed {deletedRowCount} rows, Updated {updatedRowCount} rows, Added {additionRowCount} rows");
            };
        }
    }
}
