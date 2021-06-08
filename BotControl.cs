﻿using Newtonsoft.Json;
using System.IO;
using System;

namespace DismantledBot
{
    public sealed class BotControl
    {
        public string Token = "";
        public ulong AdminID = 0;

        public char Prefix = '~';
        public string SettingsPath = "Settings\\";

        public string LogPath = "Logs\\";
        public int LogLevel = 0;

        public string DataSource = "ORCL";
        public string DBTNSAdminLocation = "%ORACLE_HOME%\\network\\admin";
        public string DBUserID = "";
        public string DBPassword = "";

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
            catch(FileNotFoundException)
            {
                Console.Error.WriteLine($"Couldn't find file @ path: [{Directory.GetCurrentDirectory()}\\{path}]");
                File.WriteAllText(path, JsonConvert.SerializeObject(new BotControl(), Formatting.Indented));
                Environment.Exit(-1);
                return null;
            } 
            catch(Exception e)
            {
                throw e;
            }
        }
    }
}
