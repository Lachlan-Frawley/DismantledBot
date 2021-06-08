using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;

namespace DismantledBot
{
    // Some fuckin settings stuff
    public sealed class ModuleSettingsManager<T>
    {
        // Each type is an instanced singleton
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

        public void Clear()
        {
            instancedSettings.Clear();
        }

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
                Utilities.PrintException(e);
                throw e;
            }
        }
      
        public P GetDataOrDefault<P>(string key, P def)
        {
            try
            {
                return data.TryGetValue(key, out object value) ? (value == null ? def : (P)Convert.ChangeType(value, typeof(P), CultureInfo.InvariantCulture)) : def;
            }
            catch (Exception e)
            {
                Utilities.PrintException(e);
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
                Utilities.PrintException(e);
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
                if (data == null)
                    data = new Dictionary<string, object>();
            }
            else
            {
                data = new Dictionary<string, object>();
                string dirPath = $"{Directory.GetCurrentDirectory()}\\{CoreProgram.settings.SettingsPath}\\";
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);
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
                CoreProgram.logger.Write(Logger.CRITICAL, "Could not find directory to save module settings!");
                throw e;
            }
            catch (FileNotFoundException e)
            {
                CoreProgram.logger.Write(Logger.CRITICAL, "Could not find file to save module settings!");
                throw e;
            }
            catch (IOException e)
            {
                throw e;
            }
        }
    }
}
