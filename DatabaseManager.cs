using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data.Odbc;
using System.Data;
using Discord;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

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

        public void ManageGuildMembers(List<IGuildUser> users)
        {           
            using(var connection = MakeConnection())
            {
                // Nickname change (update)
                List<IGuildUser> updatedNames = new List<IGuildUser>();
                // Joined server (insert)
                List<IGuildUser> newNames = new List<IGuildUser>();
                // Left/Changed username (delete)
                List<ulong> missingNames = new List<ulong>();       

                connection.Open();
                OracleCommand getIds = new OracleCommand("SELECT DISCORDID, USERNAME, NICKNAME FROM GUILDMEMBERS", connection);
                OracleDataReader allIds = getIds.ExecuteReader();

                Dictionary<ulong, string> nickData = new Dictionary<ulong, string>();
                Dictionary<ulong, string> usernameData = new Dictionary<ulong, string>();
                while (allIds.Read())
                {
                    byte[] idBytes = allIds.Get<byte[]>(0);
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
                    var param = removeCommand.Parameters.Add("DiscordID", BitConverter.GetBytes(x));
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

        public long? QueryEventIDFromMessageID(ulong? messageID)
        {
            if (!messageID.HasValue)
                return null;

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand findEventID = new OracleCommand("SELECT EVENTID FROM Events WHERE MessageID = :MessageID", connection);
                findEventID.Parameters.Add("MessageID", BitConverter.GetBytes(messageID.Value));

                object response = findEventID.ExecuteScalar();
                if (response == null)
                    return null;
                return Convert.ToInt64(response);
            }
        }

        public int QueryEventSignupCount(ulong? messageID)
        {
            if (!messageID.HasValue)
                return -1;

            long? eventID = QueryEventIDFromMessageID(messageID);
            if (!eventID.HasValue)
                return -2;

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand countCommand = new OracleCommand("SELECT COUNT(*) FROM CurrentEventSignup WHERE EventID = :EventID", connection);
                countCommand.Parameters.Add("EventID", BitConverter.GetBytes(eventID.Value));

                object response = countCommand.ExecuteScalar();
                if (response == null)
                    return -3;
                return Convert.ToInt32(response);
            }
        }

        // Not working for now
        public long QueryNextEventSignupOrder(ulong? messageID)
        {
            return 0;

            if (!messageID.HasValue)
                return -1;

            long? eventID = QueryEventIDFromMessageID(messageID);
            if (!eventID.HasValue)
                return -2;

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand("SELECT SignupOrder FROM CurrentEventSignup WHERE EventID = :EventID ORDER BY SignupOrder DESC", connection);
                query.Parameters.Add("EventID", BitConverter.GetBytes(messageID.Value));

                object value = query.ExecuteScalar();
                if (value == null)
                    return -3;
                return Convert.ToInt64(value);
            }
        }

        public bool IsUserInSignup(long eventID, ulong userID)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand("SELECT COUNT(*) FROM CurrentEventSignup WHERE EventID = :EventID AND DiscordID = :DiscordID", connection);
                query.Parameters.Add("EventID", eventID);
                query.Parameters.Add("DiscordID", BitConverter.GetBytes(userID));

                object value = query.ExecuteScalar();
                if (value == null)
                    return false;
                long amount = (long)value;
                return amount != 0;
            }
        }

        public void RemoveUserFromSignup(long eventID, ulong userID)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand("DELETE FROM CurrentEventSignup WHERE EventID = :EventID AND DiscordID = :DiscordID", connection);
                query.Parameters.Add("EventID", eventID);
                query.Parameters.Add("DiscordID", BitConverter.GetBytes(userID));
                int modifications = query.ExecuteNonQuery();
                Console.WriteLine($"Removed {modifications} row(s)");
            }
        }

        public void AddUserToSignup(long eventID, ulong userID, long signupOrder)
        {
            using (var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand query = new OracleCommand("INSERT INTO CurrentEventSignup (EventID, DiscordID, SignupOrder) VALUES (:EventID, :DiscordID, :SignupOrder);", connection);
                query.Parameters.Add("EventID", eventID);
                query.Parameters.Add("DiscordID", BitConverter.GetBytes(userID));
                query.Parameters.Add("SignupOrder", signupOrder);
                int modifications = query.ExecuteNonQuery();
                Console.WriteLine($"Added {modifications} row(s)");
            }
        }

        // TODO
        public List<string> QueryEventSignupNames(ulong? messageID)
        {
            List<string> names = new List<string>();
            if (!messageID.HasValue)
                return names;

            long? eventID = QueryEventIDFromMessageID(messageID);
            if (!eventID.HasValue)
                return names;

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand readCommand = new OracleCommand("TODO", connection);
            }

            return names;
        }

        public List<EventDataObject> QueryAllEventInformation()
        {
            List<EventDataObject> allEvents = new List<EventDataObject>();

            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleCommand eventsQuery = new OracleCommand(@"SELECT e.ChannelID, e.MessageID, e.EventName, e.EventDescription, e.MaxParticipants, e.EventLength, e.AcceptedEmoteID, t.TimeZone, t.EventTime, s.RepeatDays, s.RepeatWeeks FROM (Events e INNER JOIN EventTimes t ON e.TimeID = t.ID) INNER JOIN EventSchedule s ON e.ScheduleID = s.ID", connection);
                OracleDataReader eventsReader = eventsQuery.ExecuteReader();
                while(eventsReader.Read())
                {
                    EventDataObject obj = new EventDataObject()
                    {
                        MessageChannelID = eventsReader.GetOrNull("ChannelID", out object mcid) ? BitConverter.ToUInt64((byte[])mcid) : (ulong?)null,
                        ExistingMessageID = eventsReader.GetOrNull("MessageID", out object emid) ? BitConverter.ToUInt64((byte[])emid) : (ulong?)null,
                        EventName = eventsReader["EventName"].ToString(),
                        EventDescription = eventsReader["EventDescription"].ToString(),
                        MaxParticipants = eventsReader.GetOrNull("MaxParticipants", out object maxp) ? Convert.ToInt32(maxp) : (int?)null,
                        EventLengthSeconds = eventsReader.GetOrNull("EventLength", out object elen) ? BitConverter.ToInt64((byte[])elen) : (long?)null,
                        AcceptedEmoteID = BitConverter.ToUInt64(eventsReader.Get<byte[]>("AcceptedEmoteID")),
                        EventTime = new EventTimeObject()
                        {
                            TimeZone = eventsReader["TimeZone"].ToString(),
                            EventTime = eventsReader.Get<DateTime>("EventTime").ExtractTimeFromDateTime()
                        },
                        EventSchedule = new EventScheduleObject()
                        {
                            ApplicableDays = (EventScheduleObject.Weekday)BitConverter.ToInt64(eventsReader.Get<byte[]>("RepeatDays")),
                            WeekRepetition = (EventScheduleObject.FourWeekRepeat)BitConverter.ToInt64(eventsReader.Get<byte[]>("RepeatWeeks"))
                            //ApplicableDays = Utilities.ConvertToFlags<EventScheduleObject.Weekday>(eventsReader["RepeatDays"].ToString(), ","),
                            //WeekRepetition = Utilities.ConvertToFlags<EventScheduleObject.FourWeekRepeat>(eventsReader["RepeatWeeks"].ToString(), ",")
                        }
                    };

                    allEvents.Add(obj);
                }
                eventsReader.Close();
            }

            return allEvents;
        }

        public void CreateEvent(EventDataObject eventData)
        {
            using(var connection = MakeConnection())
            {
                connection.Open();
                OracleTransaction eventCreationTransaction = connection.BeginTransaction();
                int modifications = 0;
                OracleCommand getInsertIdentity = new OracleCommand("SELECT @@IDENTITY;", connection);
                getInsertIdentity.Transaction = eventCreationTransaction;

                OracleCommand createEventTime = new OracleCommand("INSERT INTO EventTimes (TimeZone, EventTime) VALUES(:TimeZone, :EventTime);", connection);
                createEventTime.Transaction = eventCreationTransaction;
                createEventTime.Parameters.Add("TimeZone", eventData.EventTime.TimeZone);
                createEventTime.Parameters.Add("EventTime", eventData.EventTime.EventTime.ToString(@"hh\:mm"));
                modifications += createEventTime.ExecuteNonQuery();                
                int eventTimeInsertionID = (int)getInsertIdentity.ExecuteScalar();

                OracleCommand createEventSchedule = new OracleCommand("INSERT INTO EventSchedule (RepeatDays, RepeatWeeks) VALUES(:RepeatDays, :RepeatWeeks);", connection);
                createEventSchedule.Transaction = eventCreationTransaction;
                createEventSchedule.Parameters.Add("RepeatDays", BitConverter.GetBytes((int)eventData.EventSchedule.ApplicableDays));
                createEventSchedule.Parameters.Add("RepeatWeeks", BitConverter.GetBytes((int)eventData.EventSchedule.WeekRepetition));
                modifications += createEventSchedule.ExecuteNonQuery();
                int eventScheduleInsertionID = (int)getInsertIdentity.ExecuteScalar();

                OracleCommand createEvent = new OracleCommand("INSERT INTO Events (ChannelID, MessageID, EventName, EventDescription, MaxParticipants, AcceptedEmoteID, EventLength, TimeID, ScheduleID) VALUES (:ChannelID, :MessageID, :EventName, :EventDescription, :MaxParticipants, :AcceptedEmoteID, :EventLength, :TimeID, :ScheduleID)", connection);
                createEvent.Transaction = eventCreationTransaction;
                createEvent.Parameters.Add("ChannelID", eventData.MessageChannelID.HasValue ? BitConverter.GetBytes(eventData.MessageChannelID.Value) : Convert.DBNull);
                createEvent.Parameters.Add("MessageID", eventData.ExistingMessageID.HasValue ? BitConverter.GetBytes(eventData.ExistingMessageID.Value) : Convert.DBNull);
                createEvent.Parameters.Add("EventName", eventData.EventName);
                createEvent.Parameters.Add("EventDescription", eventData.EventDescription);
                createEvent.Parameters.Add("MaxParticipants", eventData.MaxParticipants ?? Convert.DBNull);
                createEvent.Parameters.Add("TimeID", eventTimeInsertionID);
                createEvent.Parameters.Add("ScheduleID", eventScheduleInsertionID);
                createEvent.Parameters.Add("EventLength", eventData.EventLengthSeconds.HasValue ? BitConverter.GetBytes(eventData.EventLengthSeconds.Value) : Convert.DBNull);
                createEvent.Parameters.Add("AcceptedEmoteID", BitConverter.GetBytes(eventData.AcceptedEmoteID));
                modifications += createEvent.ExecuteNonQuery();
                
                eventCreationTransaction.Commit();
                Console.WriteLine($"Event creation completed: {modifications} rows affected");
            }
        }
    }
}
