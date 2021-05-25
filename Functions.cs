using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DismantledBot
{
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
        }

        public static class Implementations
        {
            public static Func<ulong> CREATOR_ID_FUNC = () => 216098427317125120;
            public static Func<ulong> GET_OWNER_ID_FUNC = () =>
            {
                ModuleSettingsManager<BindingModule> bindSettings = ModuleSettingsManager<BindingModule>.MakeSettings();
                ulong ownerID = bindSettings.GetData<ulong>(BindingModule.BINDING_OWNER_KEY);

                return ownerID == 0 ? CREATOR_ID_FUNC() : ownerID;
            };
        }
    }
}
