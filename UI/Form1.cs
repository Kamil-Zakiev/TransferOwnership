using Google.Apis.Drive.v3;
using System;
using System.Windows.Forms;
using Google.Apis.Services;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace UI
{
    public partial class Form1 : Form
    {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/drive-dotnet-quickstart.json
        static string[] Scopes = { DriveService.Scope.Drive  };

        static string ApplicationName = "Drive API .NET Quickstart";
            
        public Form1()
        {
            InitializeComponent();
        }

        private IGoogleService OldOwnerGoogleService;
        private IGoogleService NewOwnerGoogleService;

        private IReadOnlyList<FileDTO> files;

        //UserCredential credential1;
        //DriveService service1;
        //DriveService service2;
        //string oldOwnerEmail;
        //string newOwnerEmail;
        //IEnumerable<Google.Apis.Drive.v3.Data.File> myFiles;

        private void QuickStartBtn_Click(object sender, EventArgs e)
        {
            var authService = new GoogleAuthorizeService("credentials.json", new[] { DriveService.Scope.Drive }, "token.json");
            var authTask = authService.Authorize();

            authTask.ContinueWith(t =>
            {
                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = t.Result,
                    ApplicationName = ApplicationName,
                });
                OldOwnerGoogleService = new GoogleService(service);

                var userInfo = OldOwnerGoogleService.GetUserInfo();
                OldOnerNameLabel.Text = userInfo.Name + " (" + userInfo.EmailAddress + ")";
                pictureBox1.Load(userInfo.PhotoLink.AbsoluteUri);

                files = OldOwnerGoogleService.GetOwnedFiles();
                textBox1.Text = "";

                var sb = new StringBuilder();
                sb.AppendLine($"Мои файлы ({files.Count} шт.): ");
                foreach (var file in files)
                {
                    sb.AppendLine($"{file.Name}");
                }

                textBox1.Text = sb.ToString();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var authService = new GoogleAuthorizeService("credentials.json", new[] { DriveService.Scope.Drive }, "token2.json");
            var authTask = authService.Authorize();

            authTask.ContinueWith(t =>
            {
                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = t.Result,
                    ApplicationName = ApplicationName,
                });
                NewOwnerGoogleService = new GoogleService(service);

                var userInfo = NewOwnerGoogleService.GetUserInfo();
                label1.Text = userInfo.Name + " (" + userInfo.EmailAddress + ")";
                pictureBox2.Load(userInfo.PhotoLink.AbsoluteUri);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void AddPermToTest_Click(object sender, EventArgs e)
        {
            var trasferTask = OldOwnerGoogleService.TransferOwnershipTo(files, NewOwnerGoogleService);
            trasferTask.ContinueWith(t =>
            {
                MessageBox.Show(null, "Успешно.", "Сообщение", MessageBoxButtons.OK, MessageBoxIcon.Information);
                files = new List<FileDTO>();
                textBox1.Text = "";
            }, TaskScheduler.FromCurrentSynchronizationContext());
            
            //var fileId = "1Is6VD6yp1DlBrE0wc2Df6mVAldwbfPpZ_eLP2BUWPTE";

            //var command = service1.Permissions.Create(new Google.Apis.Drive.v3.Data.Permission
            //{
            //   Role = "owner",
            //   Type = "user",
            //   EmailAddress = newOwnerEmail               
            //}, fileId);

            //command.TransferOwnership = true;
            //// google restricts from skipping notification
            //// command.SendNotificationEmail = false;
            //var perm = command.Execute();

            //var file = myFiles.Single(f => f.Id == fileId);
            //var oldUserPermId = file.Owners.First(o => o.EmailAddress == oldOwnerEmail).PermissionId;

            //var deleteCommand = service2.Permissions.Delete(fileId, oldUserPermId);
            //deleteCommand.Execute();

            //MessageBox.Show(null, "Успешно.", "Сообщение", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
