using Discord.Commands;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;

namespace DismantledBot
{
    public sealed class ModuleSettingsManager<T> where T : ModuleBase<SocketCommandContext>
    {
        private static ModuleSettingsManager<T> instancedSettings = null;

        public static ModuleSettingsManager<T> MakeSettings()
        {
            if(instancedSettings == null)
            {
                instancedSettings = new ModuleSettingsManager<T>();
                instancedSettings.Load();
            }

            return instancedSettings;
        }

        private ModuleSettingsManager()
        {

        }

        private Dictionary<string, object> data;

        public void SetData(string key, object value, bool save = true)
        {
            if(data.ContainsKey(key))
                data[key] = value;
            else
                data.Add(key, value);
        
            if (save)
                Save();
        }

        public Dictionary<string, string> GetAllDataAsString()
        {
            return data.ToDictionary(x => x.Key, x => x.Value.ToString());
        }

        public P GetReference<P>(string key) where P : class
        {
            try
            {
                return data.TryGetValue(key, out object value) ? value as P : null;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
                throw e;
            }
        }

        public P GetData<P>(string key)
        {
            try
            {
                return data.TryGetValue(key, out object value) ? (P)Convert.ChangeType(value, typeof(P), CultureInfo.InvariantCulture) : default;
            } 
            catch(Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
                throw e;
            }
        }

        public string GetPath()
        {
            return $"{Directory.GetCurrentDirectory()}\\{CoreProgram.settings.SettingsPath}\\{typeof(T).FullName}.json";
        }

        private void Load()
        {
            if (File.Exists(GetPath()))
            {
                data = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(GetPath()));
            }
            else
            {
                data = new Dictionary<string, object>();
                File.Create(GetPath()).Close();
            }            
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(GetPath(), JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (DirectoryNotFoundException e)
            {
                // Log error
                throw e;
            }
            catch (IOException e)
            {
                throw e;
            }
        }
    }
}
