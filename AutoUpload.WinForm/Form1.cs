using System.Linq;
using System.Text.Json;

using AutoUpload.Models;

using log4net;

namespace AutoUpload.WinForm
{
    public partial class Form1 : Form
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));
        private FileSystemWatcher watcher;
        private readonly string[] allowedExtensions = new[] { ".txt", ".jpg", ".png" };


        public Form1()
        {
            InitializeComponent();
            Init();
        }

        public bool Init()
        {
            log.Info($"Initializing Form1...");

            #region Reading&Binding Settings
            log.Info($"Reading Settings: <LastPath>...");
            var lastPath = Properties.Settings.Default.LastPath;
            if (Directory.Exists(lastPath))
            {
                txtPath.Text = lastPath;
                log.Info($"Read Settings: <LastPath> values {lastPath}");
                InitWatcher(lastPath);
            }
            else log.Warn($"Read Settings: <LastPath> not exists, please select a valid path.");
            #endregion

            #region Controller Initialization
            this.txtPath.ReadOnly = true;

            this.notifyIcon = new NotifyIcon();
            this.contextMenuStrip = new ContextMenuStrip();
            this.menuShow = new ToolStripMenuItem("Show");
            this.menuExit = new ToolStripMenuItem("Exit");

            this.contextMenuStrip.Items.AddRange(new ToolStripItem[] {
                this.menuShow,
                this.menuExit
            });

            this.notifyIcon.Icon = SystemIcons.Application;
            this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
            this.notifyIcon.Visible = true;
            this.notifyIcon.Text = "AutoLoad By GZ4nna";
            this.notifyIcon.DoubleClick += (s, e) => ShowMainWindow();

            this.menuShow.Click += (s, e) => ShowMainWindow();
            this.menuExit.Click += (s, e) => Application.Exit();
            #endregion

            log.Info($"Finished Initialize!");
            return true;
        }

        private void btnBrowsePath_Click(object sender, EventArgs e)
        {
            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.LastPath = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.Save();  
                log.Info($"Choose Path��{folderBrowserDialog.SelectedPath}");
                InitWatcher(folderBrowserDialog.SelectedPath);                
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; 
                this.Hide();
                notifyIcon.ShowBalloonTip(1000, "��̨������", "��������С��������", ToolTipIcon.Info);
            }
            base.OnFormClosing(e);
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void InitWatcher(string path)
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;

            watcher.EnableRaisingEvents = true;
            UpdateFileRecord();
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            log.Info($"[FileRenamed] {e.ChangeType}: {e.FullPath}");
            //MessageBox.Show($"[FileRenamed] {e.ChangeType}: {e.FullPath}");
            UpdateFileRecord();
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            log.Info($"[FileChanged] {e.ChangeType}: {e.FullPath}");
            //MessageBox.Show($"[FileChanged] {e.ChangeType}: {e.FullPath}");
            UpdateFileRecord();
        }

        private void UpdateFileRecord()
        {
            string watchPath = txtPath.Text;
            if (!Directory.Exists(watchPath)) return;

            // All files in the watch directory with allowed extensions
            var currentFiles = Directory
                .EnumerateFiles(watchPath)
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => Path.GetFileName(f))
                .ToList();

            // Load uploaded file names (if not exists, it will be empty)
            var uploadedFiles = new HashSet<string>();
            if (File.Exists(UploadTrackerPaths.UploadedPath))
            {
                try
                {
                    var uploadedJson = File.ReadAllText(UploadTrackerPaths.UploadedPath);
                    uploadedFiles = JsonSerializer.Deserialize<HashSet<string>>(uploadedJson) ?? new();
                }
                catch
                {
                    uploadedFiles = new();
                    log.Warn("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
                }
            }
            else
            {
                uploadedFiles = new();
                File.WriteAllText(UploadTrackerPaths.UploadedPath, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info("�״����У��ѳ�ʼ�� uploaded.json");
            }

            // select files that are in the current directory but not in the uploaded set
            var pendingFiles = currentFiles
                .Where(name => !uploadedFiles.Contains(name))
                .ToList();

            var state = new UploadState
            {
                AllFilesInFolder = currentFiles,
                FilesToUpload = pendingFiles
            };

            var pendingJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UploadTrackerPaths.PendingPath, pendingJson);

            log.Info($"��ظ��£����ļ� {currentFiles.Count} �������ϴ� {pendingFiles.Count} ��");
        }

        private async void btnUpload_Click(object sender, EventArgs e)
        {
            string watchPath = txtPath.Text;
            List<string> pendingFiles = new();
            if (File.Exists(UploadTrackerPaths.PendingPath))
            {
                try
                {
                    var pendingJson = await File.ReadAllTextAsync(UploadTrackerPaths.PendingPath);
                    var state = JsonSerializer.Deserialize<UploadState>(pendingJson);
                    if (state == null || state.FilesToUpload.Count == 0)
                    {
                        MessageBox.Show("û�д��ϴ����ļ�");
                        return;
                    }
                    pendingFiles = state.FilesToUpload.Select(x =>
                        Path.Combine(watchPath, x)).ToList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("��ȡ���ϴ��ļ��б�ʧ�ܣ�" + ex.Message);
                    log.Error("��ȡ���ϴ��ļ��б�ʧ��", ex);
                    return;
                }
            }
                        
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-Tenant-Id", "1627160160700962818");
            client.DefaultRequestHeaders.Add("X-Trace-Id", "12345678");
            client.DefaultRequestHeaders.Add("X-User-Id", "1433719562612506626");
            client.DefaultRequestHeaders.Add("X-User-Name", "aa");

            using var form = new MultipartFormDataContent();
            foreach (var file in pendingFiles)
            {
                if (!File.Exists(file))
                {
                    log.Warn($"�ļ� {file} �����ڣ������ϴ�");
                    continue;
                }
                var fileContent = new ByteArrayContent(File.ReadAllBytes(file));
                fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
                form.Add(fileContent, "file", Path.GetFileName(file));
            }

            try
            {
                var response = await client.PostAsync("http://10.101.16.30:32767/dp-oss/api/v1/file-storages/upload/param", form);
                var result = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("�ϴ��ɹ�");
                    log.Info($"�ļ��ϴ��ɹ������������أ�{result}");
                }
                else
                {
                    MessageBox.Show($"�ϴ�ʧ�ܣ�{response.StatusCode}\n{result}");
                    Clipboard.SetText(result);
                    log.Warn($"�ļ��ϴ�ʧ�ܣ����أ�{result}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("�ϴ�����" + ex.Message);
                log.Error("�ϴ��쳣", ex);
            }
        }
    }
}
