using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoUpload.Models;
using AutoUpload.Models.ResponseModels;

using log4net;

namespace AutoUpload.WinForm
{
    public partial class Form1 : Form
    {
        #region parameters
        // addin
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));

        // settings
        /// <summary>
        /// Allowed file extensions for monitoring and uploading.
        /// todo: get from settings or config file
        /// </summary>
        private string[]? allowedExtensions;
        /// <summary>
        /// Allowed file name rules for monitoring and uploading.
        /// </summary>
        private string allowedFileNameRules;

        // param
        /// <summary>
        /// NotifyIcon for system tray interaction.
        /// </summary>
        private FileSystemWatcher? watcher;
        /// <summary>
        /// Target URL for file upload.
        /// </summary>
        private string? targetURL;
        private string? writeURL;
        #endregion

        #region Init
        public Form1()
        {
            InitializeComponent();
            Init();
            InitWatcher(txtPath.Text);
        }

        /// <summary>
        /// Initializes the Form1 instance, reading settings and binding controls.
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            log.Info($"Initializing Form1...");

            try
            {
                #region Reading&Binding Settings
                log.Info($"Reading Settings: <LastPath>...");
                var lastPath = Properties.Settings.Default.LastPath;
                if (Directory.Exists(lastPath))
                {
                    txtPath.Text = lastPath;
                    log.Info($"Read Settings: <LastPath> values {lastPath}");                    
                }
                else log.Warn($"Read Settings: <LastPath> not exists, please select a valid path.");

                log.Info($"Reading Settings: <TargetURL>...");
                targetURL = Properties.Settings.Default.TargetURL;
                log.Info($"Read Settings: <TargetURL> values {targetURL}");

                log.Info($"Reading Settings: <WriteURL>...");
                writeURL = Properties.Settings.Default.WriteURL;
                log.Info($"Read Settings: <WriteURL> values {writeURL}");

                log.Info($"Reading Settings: <AllowedExtensions>...");
                allowedExtensions = Properties.Settings.Default.AllowedExtensions.Split("|");
                log.Info($"Reading Settings: <allowedExtensions> values {allowedExtensions}");

                log.Info($"Reading Settings: <AllowedFileNameRules>...");
                allowedFileNameRules = Properties.Settings.Default.AllowedFileNameRules;
                log.Info($"Reading Settings: <AllowedFileNameRules> values {allowedFileNameRules}");
                #endregion

                #region Controls Initialization
                log.Info($"Initializing Controls...");
#if DEBUG
                this.txtPath.ReadOnly = false;
#else
                this.txtPath.ReadOnly = true; // prevent user from changing path in release mode
#endif

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
                log.Info($"Finished Controls Initialize!");
#endregion
            }
            catch(Exception ex)
            {
                log.Error($"Initializing Form1 Failed: {ex.Message}");
            }
            log.Info($"Finished Form1 Initialize!");
            return true;
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
        #endregion

        #region UI Event
        /// <summary>
        /// Handles the Browse button click event to select a directory for monitoring.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Uploads files listed in the pending.json file to the target URL.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnUpload_Click(object sender, EventArgs e)
        {
            // ������ϴ��ļ��б�
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

            // �����ϴ��ӿ�
            using var sendClient = new HttpClient();
            sendClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
            sendClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
            sendClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
            sendClient.DefaultRequestHeaders.Add("X-User-Name", Properties.Settings.Default.XUserName);

            // �����ͺŲ�ѯ��ģID
            using var queryClient = new HttpClient();

            // �ϴ��ļ����õ�����Ӧ
            var responseModels = new List<FileStorageUploadParamResponseModel>();

            foreach (var file in pendingFiles)
            {
                if (!File.Exists(file))
                {
                    log.Warn($"�ļ� {file} �����ڣ������ϴ�");
                    continue;
                }

                // ��ȡ�ļ��������
                string[]? fileNameParts = Path.GetFileNameWithoutExtension(file)?.Split(' ');
                // ��getȥ���ͺŹ���Ӧ��ID
                var queryResponse = await queryClient.GetAsync($"{writeURL}?partsCode={fileNameParts[0]}&specification={fileNameParts[1]}");
                var queryResult = await queryResponse.Content.ReadAsStringAsync();
                if (!queryResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show($"��ѯʧ�ܣ�{queryResponse.StatusCode}\n{queryResult}");
                    log.Warn($"��ѯʧ�ܣ�{queryResult}");
                    continue;
                }
                //get���
                var queryData = JsonSerializer.Deserialize<FileStorageUploadParamResponseModel>(queryResult);
                // ��������е���������,˵�����µ�
                if (queryData.data.Count < int.Parse(fileNameParts[3]))
                {

                }
                // �ɵĲ���Ҫ���Ӵ�����Ϣ
                else
                {
                    
                }

                    var fileContent = new ByteArrayContent(File.ReadAllBytes(file));
                fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
                var form = new MultipartFormDataContent();
                form.Add(fileContent, "file", Path.GetFileName(file));

                var response = await sendClient.PostAsync(targetURL, form);
                var result = await response.Content.ReadAsStringAsync();

                try
                {
                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Upload Completed{response}");
                        log.Info($"Upload Completed:{result}");
                        // ��result����FileStorageUploadParamResponseModel������
                        var uploadResponse = JsonSerializer.Deserialize<FileStorageUploadParamResponseModel>(result);
                        responseModels.Add(uploadResponse);
                    }
                    else
                    {
                        MessageBox.Show($"Upload Failed:{response.StatusCode}\n{result}");
#if DEBUG
                        Clipboard.SetText(result);
#endif
                        log.Warn($"Upload Failed:{result}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("�ϴ�����" + ex.Message);
                    log.Error("�ϴ��쳣", ex);
                }                
            }
                        
            // ����д��ӿ�
            using var writeClient = new HttpClient();
            writeClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
            writeClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
            writeClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
            writeClient.DefaultRequestHeaders.Add("X-User-Name", Properties.Settings.Default.XUserName);


            // ���� uploaded.json �ļ�
        }
        
        #endregion

        #region Form Event
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
        #endregion

        #region FileSystemWatcher Events
        /// <summary>
        /// Renamed event handler for FileSystemWatcher.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            log.Info($"[FileRenamed] {e.ChangeType}: {e.FullPath}");
            UpdateFileRecord();
        }

        /// <summary>
        /// Changed event handler for FileSystemWatcher.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            log.Info($"[FileChanged] {e.ChangeType}: {e.FullPath}");
            // MessageBox.Show($"[FileChanged] {e.ChangeType}: {e.FullPath}");
            UpdateFileRecord();
        }

        /// <summary>
        /// Updates the file record by checking the current files in the watch directory
        /// </summary>
        private void UpdateFileRecord()
        {
            string watchPath = txtPath.Text;
            if (!Directory.Exists(watchPath)) return;

            // Get all files in the watch directory with allowed extensions and matching file name rules
            var currentFiles = Directory
                .EnumerateFiles(watchPath)
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => Path.GetFileName(f))
                .Where(name => Regex.Match(name, @allowedFileNameRules).Success)
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

        #endregion

        #region
        #endregion

    }
}
