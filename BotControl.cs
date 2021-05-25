using Newtonsoft.Json;
using System.IO;
using System;

namespace DismantledBot
{
    public sealed class BotControl
    {
        public string Token;

        public char Prefix = '~';
        public string SettingsPath = "Settings\\";

        public string LogPath = "Logs\\";
        public int LogLevel = 0;

        private BotControl()
        {

        }

        public static BotControl Load(string path)
        {
            try
            {
                string text = File.ReadAllText(path);
                BotControl settings = JsonConvert.DeserializeObject<BotControl>(text);
                return settings;
            } 
            catch(FileNotFoundException e)
            {
                Console.Error.WriteLine($"Couldn't find file @ path: [{Directory.GetCurrentDirectory()}\\{path}]");
                File.WriteAllText(path, JsonConvert.SerializeObject(new BotControl(), Formatting.Indented));
                throw e;
            } 
            catch(Exception e)
            {
                throw e;
            }
        }
    }
}
