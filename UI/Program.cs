using Google.Apis.Drive.v3;
using System;
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

            // var configFilePath = ConfigurationManager.AppSettings["GoogleAppConfigFile"];
            var oldAuthService = new GoogleAuthorizeService("client_id.json", new[] { DriveService.Scope.Drive }, "token.json");
            var newAuthService = new GoogleAuthorizeService("client_id.json", new[] { DriveService.Scope.Drive }, "token2.json");

            var logger = new FileLogger("log.txt");
            var expBackoffPolicy = new ExpBackoffPolicy(logger);

            Application.Run(new Form1(oldAuthService, newAuthService, expBackoffPolicy, logger));
        }
    }
}
