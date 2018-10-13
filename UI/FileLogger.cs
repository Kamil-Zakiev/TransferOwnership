using System;
using System.IO;

namespace UI
{
    public interface ILogger
    {
        void LogMessage(string message);
    }

    internal class FileLogger : ILogger
    {
        private string NowStr => DateTime.Now.ToString();

        private string _filePath;

        public FileLogger(string filePath, string defaultFile)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)
                ? defaultFile
                : filePath;

            File.WriteAllLines(_filePath, new[] { $"[{NowStr}]: Программа запущена на выполнение" });
        }

        public void LogMessage(string message)
        {
            File.AppendAllLines(_filePath, new[] { $"[{NowStr}]: {message}" });
        }
    }
}
