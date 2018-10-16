using Google.Apis.Drive.v3;
using System;
using System.Windows.Forms;
using Google.Apis.Services;
using System.Text;
using System.Collections.Generic;
using System.Linq;
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
            var formLogger = new FormLoggerDecorator(logger, textBox2);
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
            var httpClientInitializer = OldOwnerAuthService.Authorize();
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = httpClientInitializer,
                ApplicationName = ApplicationName,
            });
            OldOwnerGoogleService = new GoogleService(service, _expBackoffPolicy, _logger);
            UpdateFileList();
        }

        private void UpdateFileList()
        {
            files = OldOwnerGoogleService.GetOwnedFiles().OrderBy(file => file.MimeType).ThenBy(file => file.Name).ToArray();
            var userInfo = OldOwnerGoogleService.GetUserInfo();

            OldOnerNameLabel.Text = userInfo.Name + " (" + userInfo.EmailAddress + ")";
            //pictureBox1.Load(userInfo.PhotoLink.AbsoluteUri);

            textBox1.Text = "";
            var sb = new StringBuilder();
            sb.AppendLine($"Мои файлы и папки ({files.Count} шт.): ");
            foreach (var file in files)
            {
                var prefix = file.MimeType == "application/vnd.google-apps.folder" ? "[Папка]: " : string.Empty;
                sb.AppendLine($"{prefix}{file.Name}");
            }

            textBox1.Text = sb.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var httpClientInitializer = NewOwnerAuthService.Authorize();
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = httpClientInitializer,
                ApplicationName = ApplicationName,
            });

            _logger.LogMessage("Создан сервис Гугла для обработки запросов");
            NewOwnerGoogleService = new GoogleService(service, _expBackoffPolicy, _logger);

            var userInfo = NewOwnerGoogleService.GetUserInfo();
            label1.Text = userInfo.Name + " (" + userInfo.EmailAddress + ")";
            // pictureBox2.Load(userInfo.PhotoLink.AbsoluteUri);
        }

        private void AddPermToTest_Click(object sender, EventArgs e)
        {
            progressBar1.Visible = true;
            progressBar1.Value = 0;
            progressBar1.Maximum = files.Count;
            label2.Text = ((double)progressBar1.Value / progressBar1.Maximum).ToString("P");

            var stopWatch = new Stopwatch();  
            stopWatch.Start(); 
            OldOwnerGoogleService.RejectRights(files, NewOwnerGoogleService, file =>
            {
                label2.Text = ((double) progressBar1.Value / progressBar1.Maximum).ToString("P");
                progressBar1.Increment(1);
            });

            stopWatch.Stop();
            UpdateFileList();
            progressBar1.Visible = false;
            var execSec = (double)stopWatch.ElapsedMilliseconds / 1000;
            MessageBox.Show(null,
                $"Перенос завершен. Время работы программы {execSec.ToString("F2")} секунд",
                "Сообщение",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
