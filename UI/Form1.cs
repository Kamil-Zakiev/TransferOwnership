using Google.Apis.Drive.v3;
using System;
using System.Windows.Forms;
using Google.Apis.Services;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace UI
{
    public partial class Form1 : Form
    {
        static string[] Scopes = { DriveService.Scope.Drive  };

        static string ApplicationName = "OwnershipTransmit";
            
        public Form1(IGoogleAuthorizeService oldOwnerAuthService, IGoogleAuthorizeService newOwnerAuthService)
        {
            InitializeComponent();
            OldOwnerAuthService = oldOwnerAuthService;
            NewOwnerAuthService = newOwnerAuthService;
        }

        private IGoogleService OldOwnerGoogleService;

        private IGoogleService NewOwnerGoogleService;

        private IGoogleAuthorizeService OldOwnerAuthService;

        private IGoogleAuthorizeService NewOwnerAuthService;

        private IReadOnlyList<FileDTO> files;

        private void QuickStartBtn_Click(object sender, EventArgs e)
        {
            var authTask = OldOwnerAuthService.Authorize();
            var serviceCreationTask = authTask.ContinueWith(t =>
            {
                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = t.Result,
                    ApplicationName = ApplicationName,
                });
                OldOwnerGoogleService = new GoogleService(service);
            });

            // need to continue only when service is initialized
            serviceCreationTask.ContinueWith(t => UpdateFileList(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UpdateFileList()
        {
            var onServiceCreatedTask = Task.Run(() =>
            {
                files = OldOwnerGoogleService.GetOwnedFiles();
                var userInfo = OldOwnerGoogleService.GetUserInfo();
                return userInfo;
            });

            onServiceCreatedTask.ContinueWith(t =>
            {
                var userInfo = t.Result;
                OldOnerNameLabel.Text = userInfo.Name + " (" + userInfo.EmailAddress + ")";
                pictureBox1.Load(userInfo.PhotoLink.AbsoluteUri);

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
            var authTask = NewOwnerAuthService.Authorize();
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
            var result = OldOwnerGoogleService.TransferOwnershipTo(files, NewOwnerGoogleService, (i, file) =>
            {
                label2.Text = (i+1) + "/" + files.Count;
            });
            var succeedCount = result.Count(r => r.Success);

            var messageBuilder = new StringBuilder($"{succeedCount} файлов из {result.Count} теперь имеют нового владельца!");
            if (succeedCount != result.Count)
            {
                messageBuilder.AppendLine(Environment.NewLine + "Остальные файлы перенести не удалось.");
            }

            MessageBox.Show(null,
                messageBuilder.ToString(), 
                "Сообщение", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Information);

            UpdateFileList();

            label2.Text = "";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OldOwnerAuthService.Clear();
            NewOwnerAuthService.Clear();

            textBox1.Text = label1.Text = OldOnerNameLabel.Text = "";
            pictureBox1.Image = pictureBox2.Image = null;
            OldOwnerGoogleService = NewOwnerGoogleService = null;
        }
    }
}
