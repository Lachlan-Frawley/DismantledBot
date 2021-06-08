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
            public const string CREATOR_ID_FUNC = "cidf";
            public const string GET_OWNER_ID_FUNC = "goidf";
            public const string OFFICER_ROLE_FUNC = "orf";
            public const string MEMBER_ROLE_FUNC = "mrf";
        }

        public static class Implementations
        {
            public static Func<ulong> OFFICER_ROLE_FUNC = () => 740394363858976818;
            public static Func<ulong> CREATOR_ID_FUNC = () => 216098427317125120;
            public static Func<ulong> GET_OWNER_ID_FUNC = () =>
            {
                ulong ownerID = 0;

                return ownerID == 0 ? CREATOR_ID_FUNC() : ownerID;
            };
            public static Func<ulong> MEMBER_ROLE_FUNC = () =>
            {
                return 0;
            };
        }
    }
}
