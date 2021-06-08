using System;
using System.IO;

namespace DismantledBot
{
    public sealed class Logger
    {
        public const int CRITICAL = 0;
        public const int MAJOR = 1;
        public const int ERROR = 2;
        public const int WARNING = 3;
        public const int DEBUG = 4;

        private readonly string LogFile;
        private readonly int LogLevel;

        public Logger(string logPath, int level)
        {
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);

            LogLevel = level;
            LogFile = $"{logPath}\\Log_{Directory.GetFiles(logPath).Length}_{DateTime.Now.ToShortDateString()}.log";
        }

        public void Write(int level, string value)
        {
            if (level > LogLevel)
                return;
            Write(value);
        }

        private void Write(string value)
        {
            File.AppendAllText(LogFile, value);
        }
    }
}
