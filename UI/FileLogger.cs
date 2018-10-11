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

        public FileLogger(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            _filePath = filePath;

            File.WriteAllLines(_filePath, new[] { $"[{NowStr}]: Программа запущена на выполнение" });
        }

        public void LogMessage(string message)
        {
            if (_filePath == null)
            {
                return;
            }

            File.AppendAllLines(_filePath, new[] { $"[{NowStr}]: {message}" });
        }
    }
}
