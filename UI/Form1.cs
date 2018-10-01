using Google.Apis.Drive.v3;
using System;
using System.Windows.Forms;
using Google.Apis.Services;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

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
            progressBar1.Visible = false;
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
            progressBar1.Visible = true;
            progressBar1.Value = 1;
            progressBar1.Maximum = files.Count;

            var syncContextScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            var transferTask = new Task<IReadOnlyList<TransferingResult>>(() => OldOwnerGoogleService.TransferOwnershipTo(files, NewOwnerGoogleService, (i, file) =>
            {
                var task = new Task(() => progressBar1.Increment(1));
                task.Start(syncContextScheduler);
            }));

            transferTask.ContinueWith(t =>
            {
                var result = t.Result;
                var loadedFiles = result.Where(r => !r.Success).Select(r => r.File).ToArray();

                if (!loadedFiles.Any())
                {
                    MessageBox.Show(null,
                           "Перенос завершен",
                           "Сообщение",
                           MessageBoxButtons.OK,
                           MessageBoxIcon.Information);

                    UpdateFileList();
                    progressBar1.Visible = false;

                    return;
                }

                var reloadTask = Task.Run(() =>
                {
                    OldOwnerGoogleService.ReloadFromNewUser(loadedFiles, NewOwnerGoogleService, (i, file) =>
                    {
                        var task = new Task(() => progressBar1.Increment(1));
                        task.Start(syncContextScheduler);
                    });
                });

                reloadTask.ContinueWith(tsk =>
                {
                    MessageBox.Show(null,
                          "Перенос завершен",
                          "Сообщение",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Information);
                    UpdateFileList();

                    progressBar1.Visible = false;
                }, syncContextScheduler);

            }, syncContextScheduler);

            transferTask.Start();
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
