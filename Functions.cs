using System;
using System.Linq;
using System.Reflection;

namespace DismantledBot
{
    // Some stuff
    public static class Functions
    {
        public static Func<T> GetFunc<T>(string name)
        {
            FieldInfo[] nameFields = typeof(Names).GetFields();
            if(nameFields.GetContains(x => x.GetValue(null).ToString().Equals(name), out FieldInfo field))
            {
                FieldInfo foundFunc = typeof(Implementations).GetFields().ToList().Find(x => x.Name.Equals(field.Name));
                if(foundFunc == null)
                {
                    return null;
                }

                return foundFunc.GetValue(null) as Func<T>;
            }

            return null;
        }

        public static class Names
        {
            public const string GET_OWNER_FUNC = "GET_OWNER_FUNC";
            public const string GET_ADMIN_FUNC = "GET_ADMIN_FUNC";
        }

        public static class Implementations
        {
            public static Func<ulong> GET_OWNER_FUNC = () =>
            {
                return SettingsModule.settings.GetData<ulong>(Names.GET_OWNER_FUNC);
            };

            public static Func<ulong> GET_ADMIN_FUNC = () =>
            {
                return CoreProgram.settings.AdminID;
            };
        }
    }
}
