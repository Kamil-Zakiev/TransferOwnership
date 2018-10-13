using Google.Apis.Drive.v3;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace UI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var configFilePath = ConfigurationManager.AppSettings["GoogleAppConfigFile"];
            if (string.IsNullOrWhiteSpace(configFilePath) || !File.Exists(configFilePath))
            {
                var jsonFiles = Directory.GetFiles(Environment.CurrentDirectory, "*.json");

                if (jsonFiles.Length > 1)
                {
                    throw new AmbiguousMatchException();
                }

                configFilePath = jsonFiles.Single();
            }

            var oldAuthService = new GoogleAuthorizeService(configFilePath, new[] { DriveService.Scope.Drive }, "token.json");
            var newAuthService = new GoogleAuthorizeService(configFilePath, new[] { DriveService.Scope.Drive }, "token2.json");

            var logger = new FileLogger(ConfigurationManager.AppSettings["logfile"], "log.txt");
            var expBackoffPolicy = new ExpBackoffPolicy(logger);

            Application.Run(new Form1(oldAuthService, newAuthService, expBackoffPolicy, logger));
        }
    }
}
