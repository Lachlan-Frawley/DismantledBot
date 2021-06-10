using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Linq;
using NodaTime;
using System.Data.Odbc;
using Discord;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Collections;
using Oracle.ManagedDataAccess.Client;

namespace DismantledBot
{
    // Taken from (https://stackoverflow.com/questions/2278525/system-timers-timer-how-to-get-the-time-remaining-until-elapse) by Mike
    public class TimerPlus : System.Timers.Timer
    {
        private DateTime m_dueTime;

        public TimerPlus() : base() => this.Elapsed += this.ElapsedAction;

        protected new void Dispose()
        {
            this.Elapsed -= this.ElapsedAction;
            base.Dispose();
        }

        public double TimeLeft => (this.m_dueTime - DateTime.Now).TotalMilliseconds;
        public new void Start()
        {
            this.m_dueTime = DateTime.Now.AddMilliseconds(this.Interval);
            base.Start();
        }

        private void ElapsedAction(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.AutoReset)
                this.m_dueTime = DateTime.Now.AddMilliseconds(this.Interval);
        }
    }

    // Some utilities
    public static class Utilities
    {
        private static Random Random = new Random();

        public static IEnumerable<AutoDBField> GetAllAutoFields<T>()
        {
            List<AutoDBField> autoDBFields = new List<AutoDBField>();
            if (typeof(T).GetAutoTable() == null)
                return autoDBFields;
            foreach(FieldInfo field in typeof(T).GetFields())
            {
                if (field.GetCustomAttribute<AutoDBNoWrite>() != null)
                    continue;
                AutoDBField aField = field.GetAutoField();
                if (aField == null)
                    continue;
                if (aField.FieldName == null)
                    aField.FieldName = field.Name;
                if (aField != null)
                    autoDBFields.Add(aField);
            }
            foreach(PropertyInfo property in typeof(T).GetProperties())
            {
                if (property.GetCustomAttribute<AutoDBNoWrite>() != null)
                    continue;
                AutoDBField aField = property.GetAutoField();
                if (aField == null)
                    continue;
                if (aField.FieldName == null)
                    aField.FieldName = property.Name;
                if (aField != null)
                    autoDBFields.Add(aField);
            }
            return autoDBFields;
        }

        public static OracleParameter AddValue<T>(this OracleParameterCollection self, T obj, string name)
        {
            FieldInfo fieldInfo = typeof(T).GetFields().Where(x => x.GetAutoField() != null).Where(x => string.Equals(x.Name, name) || string.Equals(x.GetAutoField().FieldName, name)).FirstOrDefault();
            PropertyInfo propInfo = typeof(T).GetProperties().Where(x => x.GetAutoField() != null).Where(x => string.Equals(x.Name, name) || string.Equals(x.GetAutoField().FieldName, name)).FirstOrDefault();
            AutoDBField dbFieldInfo = null;
            if (fieldInfo != null)
                dbFieldInfo = fieldInfo.GetAutoField();
            if (propInfo != null)
                dbFieldInfo = propInfo.GetAutoField();
            if (dbFieldInfo == null)
                return null;

            object fieldValue = fieldInfo != null ? fieldInfo.GetValue(obj) : propInfo.GetValue(obj);
            OracleParameter param = new OracleParameter()
            {
                ParameterName = dbFieldInfo.FieldName ?? (fieldInfo != null ? fieldInfo.Name : propInfo.Name),
                OracleDbType = dbFieldInfo.FieldType,
                Size = dbFieldInfo.FieldSize,
                Value = DBConvert(fieldValue, dbFieldInfo.FieldType)
            };

            self.Add(param);
            return param;
        }

        public static object DBConvert(object input, OracleDbType toType)
        {
            switch(toType)
            {
                case OracleDbType.Decimal:
                    return Convert.ChangeType(input, typeof(Decimal));
            }

            return input;
        }

        #region AutoDB Helper
        public static bool IsValidAutoDBClass(this object self)
        {
            return IsValidAutoDBClass(self.GetType());
        }
        public static bool IsValidAutoDBClass(this Type self)
        {            
            AutoDBTable table = self.GetAutoTable();
            return table != null && !string.IsNullOrEmpty(table.TableName) && self.GetConstructor(new Type[] { }) != null;
        }
        public static AutoDBField GetAutoField(this PropertyInfo self)
        {
            return self.GetCustomAttribute<AutoDBField>();
        }
        public static AutoDBField GetAutoField(this FieldInfo self)
        {
            return self.GetCustomAttribute<AutoDBField>();
        }
        public static AutoDBTable GetAutoTable(this PropertyInfo self)
        {
            return GetAutoTable(self.DeclaringType);
        }
        public static AutoDBTable GetAutoTable(this FieldInfo self)
        {
            return GetAutoTable(self.DeclaringType);
        }
        public static AutoDBTable GetAutoTable(this object self)
        {
            return GetAutoTable(self.GetType());
        }
        public static AutoDBTable GetAutoTable(this Type self)
        {
            return self.GetCustomAttribute<AutoDBTable>();
        }
        public static object ForDB(object input)
        {
            return input ?? Convert.DBNull;
        }
        #endregion
        #region AutoDB Bot Types Helper
        public static DatabaseDataTypes.GuildMember FromIGuildUser(this IGuildUser self)
        {
            return new DatabaseDataTypes.GuildMember(self);
        }
        #endregion

        public static void PrintException(Exception e)
        {
            Console.WriteLine(e.GetType());
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }

        public static bool FindOut<T>(this IEnumerable<T> self, Func<T, bool> predicate, out T value)
        {
            IEnumerable<T> matches = self.Where(predicate);
            value = matches.FirstOrDefault();
            return matches.Count() != 0;
        }

        public static T ConvertToFlags<T>(ulong input) where T : Enum
        {
            return (T)(object)input;
        }

        public static T ConvertToFlags<T>(string input, string seperator) where T : Enum
        {
            int bits = 0;
            Array enumValues = Enum.GetValues(typeof(T));
            string[] splits = input.Split(seperator);
            foreach(string flagValue in splits)
            {
                foreach(T flag in enumValues)
                {
                    if(string.Equals(flagValue, flag.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        bits |= Convert.ToInt32(flag);
                        break;
                    }
                }
            }
            return (T)(object)bits;
        }

        public static T[] ExtractFlags<T>(T value) where T : Enum
        {
            Array enumValues = Enum.GetValues(typeof(T));
            uint activeFlags = System.Runtime.Intrinsics.X86.Popcnt.PopCount(Convert.ToUInt32(value));
            T[] rArry = new T[activeFlags];
            int index = 0;
            foreach(T flag in enumValues)
            {
                if(value.HasFlag(flag))
                {
                    rArry[index++] = flag;
                }
            }
            return rArry;
        }

        public static TimeSpan ExtractTimeFromDateTime(this DateTime self)
        {
            return new TimeSpan(0, self.Hour, self.Minute, self.Second, self.Millisecond);
        }

        public static bool GetOrNull(this OdbcDataReader self, string name, out object value)
        {
            value = self[name];
            return value != null && value != Convert.DBNull;
        }

        public static T Get<T>(this OdbcDataReader self, string name)
        {
            return (T)Convert.ChangeType(self[name], typeof(T));
        }

        public static T Get<T>(this OdbcDataReader self, int i)
        {
            return (T)Convert.ChangeType(self[i], typeof(T));
        }

        public static bool GetOrNull(this Oracle.ManagedDataAccess.Client.OracleDataReader self, string name, out object value)
        {            
            value = self[name];
            return value != null && value != Convert.DBNull;
        }

        public static T Get<T>(this Oracle.ManagedDataAccess.Client.OracleDataReader self, string name)
        {
            return (T)Convert.ChangeType(self[name], typeof(T));
        }

        public static T Get<T>(this Oracle.ManagedDataAccess.Client.OracleDataReader self, int i)
        {
            return (T)Convert.ChangeType(self[i], typeof(T));
        }

        public static LocalDateTime ConvertDateTimeToDifferentTimeZone(
            LocalDateTime fromLocal,
            string fromZoneId,
            string toZoneId)
        {
            DateTimeZone fromZone = DateTimeZoneProviders.Tzdb[fromZoneId];
            ZonedDateTime fromZoned = fromLocal.InZoneLeniently(fromZone);

            DateTimeZone toZone = DateTimeZoneProviders.Tzdb[toZoneId];
            ZonedDateTime toZoned = fromZoned.WithZone(fromZone);
            return toZoned.LocalDateTime;
        }

        public static string MatchName(string name, List<(string, string)> allGuildMembers)
        {
            if(allGuildMembers.Any(x => name.Equals(x.Item1) || name.Equals(x.Item2)))
            {
                return name;
            }

            return FuzzySearch(allGuildMembers.Select(x => x.Item1).Union(allGuildMembers.Select(x => x.Item2)), name).First();
        }

        public static List<string> MatchNames(IEnumerable<string> names, List<(string, string)> allGuildMembers)
        {
            List<string> flatMembers = allGuildMembers.Select(x => x.Item1).Union(allGuildMembers.Select(x => x.Item2)).ToList();

            List<string> MatchedNames = new List<string>();
            foreach(string name in names)
            {
                if (MatchedNames.Contains(name))
                    continue;

                if(flatMembers.Contains(name))
                {
                    MatchedNames.Add(name);
                }
                else
                {
                    List<string> fuzzy = FuzzySearch(flatMembers, name);
                    MatchedNames.Add(fuzzy.First());
                }
            }
            return MatchedNames;
        }

        public static int PMod(int x, int m)
        {
            return ((x % m) + m) % m;
        }

        public static List<string> FuzzySearch(this IEnumerable<string> self, string compare)
        {
            return self.OrderBy(x => FuzzyMatch(compare, x)).ToList();
        }

        // Find the edit distance between 2 strings via the full matrix method
        public static int FuzzyMatch(string original, string comparedTo)
        {
            int l1 = original.Length;
            int l2 = comparedTo.Length;

            int[,] data = new int[original.Length + 1, comparedTo.Length + 1];

            for (int i = 1; i <= original.Length; i++)
                data[i, 0] = i;
            for (int i = 1; i <= comparedTo.Length; i++)
                data[0, i] = i;
            for(int x = 1; x <= original.Length; x++)
            {
                for(int y = 1; y <= comparedTo.Length; y++)
                {
                    if (original[x - 1] == comparedTo[y - 1])
                        data[x, y] = data[PMod(x - 1, l1 + 1), PMod(y - 1, l2 + 1)];
                    else
                        data[x, y] = Math.Min(data[x, PMod(y - 1, l2 + 1)] + 1, Math.Min(data[PMod(x - 1, l1 + 1), y] + 1, data[PMod(x - 1, l1 + 1), PMod(y - 1, l2 + 1)] + 1));
                }
            }

            return data[original.Length, comparedTo.Length];
        }

        public static T GetRandom<T>(this IEnumerable<T> self)
        {
            if (self.Count() == 0)
                return default;

            return self.ElementAt(Random.Next(0, self.Count() - 1));
        }

        public static string GetMethod([CallerMemberName]string method = "")
        {
            return method;
        }

        public static bool GetContains<T>(this IEnumerable<T> self, T obj, out T retrieved)
        {
            foreach(T item in self)
            {
                if(Equals(item, obj))
                {
                    retrieved = item;
                    return true;
                }
            }

            retrieved = default;
            return false;
        }

        public static bool GetContains<T>(this IEnumerable<T> self, Func<T, bool> selector, out T retrieved)
        {
            foreach (T item in self)
            {
                if (selector(item))
                {
                    retrieved = item;
                    return true;
                }
            }

            retrieved = default;
            return false;
        }
    }
}
