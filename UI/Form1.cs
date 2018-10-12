using Google.Apis.Drive.v3;
using System;
using System.Windows.Forms;
using Google.Apis.Services;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace UI
{
    public partial class Form1 : Form
    {
        static string[] Scopes = { DriveService.Scope.Drive  };

        static string ApplicationName = "OwnershipTransmit";

        private readonly IExpBackoffPolicy _expBackoffPolicy;
        private readonly ILogger _logger;

        public Form1(IGoogleAuthorizeService oldOwnerAuthService, IGoogleAuthorizeService newOwnerAuthService, IExpBackoffPolicy expBackoffPolicy, ILogger logger)
        {
            _expBackoffPolicy = expBackoffPolicy ?? throw new ArgumentNullException(nameof(expBackoffPolicy));

            InitializeComponent();
            var formLogger = new FormLoggerDecorator(logger, textBox2, TaskScheduler.FromCurrentSynchronizationContext());
            _logger = formLogger;

            label2.Text = string.Empty;
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
                OldOwnerGoogleService = new GoogleService(service, _expBackoffPolicy, _logger);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            serviceCreationTask.ContinueWith(t => UpdateFileList(), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UpdateFileList()
        {
            var onServiceCreatedTask = Task.Run(() =>
            {
                files = OldOwnerGoogleService.GetOwnedFiles().OrderBy(file => file.MimeType).ThenBy(file => file.Name).ToArray();
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
                sb.AppendLine($"Мои файлы и папки ({files.Count} шт.): ");
                foreach (var file in files)
                {
                    var prefix = file.MimeType == "application/vnd.google-apps.folder" ? "[Папка]: " : string.Empty;
                    sb.AppendLine($"{prefix}{file.Name}");
                }

                textBox1.Text = sb.ToString();
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
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
                NewOwnerGoogleService = new GoogleService(service, _expBackoffPolicy, _logger);

                var userInfo = NewOwnerGoogleService.GetUserInfo();
                label1.Text = userInfo.Name + " (" + userInfo.EmailAddress + ")";
                pictureBox2.Load(userInfo.PhotoLink.AbsoluteUri);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void AddPermToTest_Click(object sender, EventArgs e)
        {
            progressBar1.Visible = true;
            progressBar1.Value = 0;
            progressBar1.Maximum = files.Count;
            label2.Text = ((double)progressBar1.Value / progressBar1.Maximum).ToString("P");

            var syncContextScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            var transferTask = new Task(() => OldOwnerGoogleService.RejectRights(files, NewOwnerGoogleService, file =>
            {
                var task = new Task(() => {
                    label2.Text = ((double)progressBar1.Value/ progressBar1.Maximum).ToString("P");
                    progressBar1.Increment(1);
                });
                task.Start(syncContextScheduler);
            }));

            var stopWatch = new Stopwatch();            
            transferTask.ContinueWith(t =>
            {
                stopWatch.Stop();
                UpdateFileList();
                progressBar1.Visible = false;
                var execSec = (double)stopWatch.ElapsedMilliseconds / 1000;
                MessageBox.Show(null,
                       $"Перенос завершен. Время работы программы {execSec.ToString("F2")} секунд)",
                       "Сообщение",
                       MessageBoxButtons.OK,
                       MessageBoxIcon.Information);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, syncContextScheduler);

            transferTask.ContinueWith(t =>
            {
                var message = Helpers.GetFullMessage(t.Exception);
                MessageBox.Show(null, message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

                UpdateFileList();
                progressBar1.Visible = false;
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, syncContextScheduler);

            transferTask.Start();
            stopWatch.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OldOwnerAuthService.Clear();
            NewOwnerAuthService.Clear();

            textBox1.Text = label1.Text = OldOnerNameLabel.Text = "";
            pictureBox1.Image = pictureBox2.Image = null;
            OldOwnerGoogleService = NewOwnerGoogleService = null;
        }

        private void progressBar1_VisibleChanged(object sender, EventArgs e)
        {
            if (!progressBar1.Visible)
            {
                label2.Text = string.Empty;
            }
        }
    }
}
