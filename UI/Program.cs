using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
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
            var oldAuthService = new GoogleAuthorizeService(configFilePath, new[] { DriveService.Scope.Drive }, "token.json");
            var newAuthService = new GoogleAuthorizeService(configFilePath, new[] { DriveService.Scope.Drive }, "token2.json");

            var logger = new FileLogger(ConfigurationManager.AppSettings["logfile"]);
            var expBackoffPolicy = new ExpBackoffPolicy(logger);

            Application.Run(new Form1(oldAuthService, newAuthService, expBackoffPolicy, logger));
        }
    }
}
