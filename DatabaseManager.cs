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
                    Console.WriteLine($"Completed guild member data update, no changed detected");
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

        public ulong? QueryEventIDFromMessageID(ulong? messageID)
        {
            if (!messageID.HasValue)
                return null;

            using(var connection = new OdbcConnection(ConnectionString))
            {
                connection.Open();
                OdbcCommand findEventID = new OdbcCommand("SELECT ID FROM Events WHERE MessageID = ?;", connection);
                findEventID.Parameters.Add(Convert.ToDecimal(messageID.Value));

                object response = findEventID.ExecuteScalar();
                if (response == null)
                    return null;
                return Convert.ToUInt32(response);
            }
        }

        public int QueryEventSignupCount(ulong? messageID)
        {
            if (!messageID.HasValue)
                return -1;

            ulong? eventID = QueryEventIDFromMessageID(messageID);
            if (!eventID.HasValue)
                return -2;

            using(var connection = new OdbcConnection(ConnectionString))
            {
                connection.Open();
                OdbcCommand countCommand = new OdbcCommand("SELECT COUNT(*) FROM CurrentEventSignup WHERE EventID = ?;", connection);
                countCommand.Parameters.Add(eventID.Value);

                object response = countCommand.ExecuteScalar();
                if (response == null)
                    return -3;
                return Convert.ToInt32(response);
            }
        }

        public List<string> QueryEventSignupNames(ulong? messageID)
        {
            List<string> names = new List<string>();
            if (!messageID.HasValue)
                return names;

            ulong? eventID = QueryEventIDFromMessageID(messageID);
            if (!eventID.HasValue)
                return names;

            using(var connection = new OdbcConnection(ConnectionString))
            {
                connection.Open();
                OdbcCommand readCommand = new OdbcCommand("TODO", connection);
            }

            return names;
        }

        public void CreateEvent(EventDataObject eventData)
        {
            using(var connection = new OdbcConnection(ConnectionString))
            {
                connection.Open();
                OdbcTransaction eventCreationTransaction = connection.BeginTransaction();
                int modifications = 0;
                OdbcCommand getInsertIdentity = new OdbcCommand("SELECT @@IDENTITY;", connection, eventCreationTransaction);

                OdbcCommand createEventTime = new OdbcCommand("INSERT INTO EventTimes (TimeZone, EventTime)\nVALUES(?, ?);", connection, eventCreationTransaction);
                createEventTime.Parameters.AddWithValue("@TimeZone", eventData.EventTime.TimeZone);
                createEventTime.Parameters.AddWithValue(@"EventTime", eventData.EventTime.EventTime.ToString(@"hh\:mm"));
                modifications += createEventTime.ExecuteNonQuery();                
                int eventTimeInsertionID = (int)getInsertIdentity.ExecuteScalar();

                OdbcCommand createEventSchedule = new OdbcCommand("INSERT INTO EventSchedule (RepeatDaysOnWeek, RepeatWeeksOn4Week)\nVALUES(?, ?);", connection, eventCreationTransaction);
                createEventSchedule.Parameters.AddWithValue("@RepeatDaysOnWeek", string.Join(',', Utilities.ExtractFlags(eventData.EventSchedule.ApplicableDays)));
                createEventSchedule.Parameters.AddWithValue("@RepeatWeeksOn4Week", string.Join(',', Utilities.ExtractFlags(eventData.EventSchedule.WeekRepetition)));
                modifications += createEventSchedule.ExecuteNonQuery();
                int eventScheduleInsertionID = (int)getInsertIdentity.ExecuteScalar();               

                OdbcCommand createEvent = new OdbcCommand("INSERT INTO Events (ChannelID, MessageID, EventName, EventDescription, MaxParticipants, TimeID, ScheduleID, EventLength, AcceptedEmoteID)\nVALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);", connection, eventCreationTransaction);
                createEvent.Parameters.AddWithValue("@ChannelID", eventData.MessageChannelID.HasValue ? Convert.ToDecimal(eventData.ExistingMessageID) : Convert.DBNull);
                createEvent.Parameters.AddWithValue("@MessageID", eventData.ExistingMessageID.HasValue ? Convert.ToDecimal(eventData.ExistingMessageID) : Convert.DBNull);
                createEvent.Parameters.AddWithValue("@EventName", eventData.EventName);
                createEvent.Parameters.AddWithValue("@EventDescription", eventData.EventDescription);
                createEvent.Parameters.AddWithValue("@MaxParticipants", eventData.MaxParticipants ?? Convert.DBNull);
                createEvent.Parameters.AddWithValue("@TimeID", eventTimeInsertionID);
                createEvent.Parameters.AddWithValue("@ScheduleID", eventScheduleInsertionID);
                createEvent.Parameters.AddWithValue("@EventLength", eventData.EventLengthSeconds ?? Convert.DBNull);
                createEvent.Parameters.AddWithValue("@AcceptedEmoteID", Convert.ToDecimal(eventData.AcceptedEmoteID));
                modifications += createEvent.ExecuteNonQuery();
                
                eventCreationTransaction.Commit();
                Console.WriteLine($"Event creation completed: {modifications} rows affected");
            }
        }
    }
}
