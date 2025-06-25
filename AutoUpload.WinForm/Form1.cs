using AutoUpload.Models;
using AutoUpload.Models.ResponseModels;
using log4net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Forms;

namespace AutoUpload.WinForm
{
    public partial class Form1 : Form
    {
        #region parameters
        // ��־
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));

        // ��settings����ȡ��
        /// <summary>
        /// �ϴ��ļ��ĺ�׺������
        /// </summary>
        private string[]? allowedExtensions;
        /// <summary>
        /// �ļ����Ϸ�����
        /// </summary>
        private string allowedFileNameRules;

        // ˽�г�Ա����
        /// <summary>
        /// �����ļ��м�����
        /// </summary>
        private FileSystemWatcher? watcher;
        /// <summary>
        /// �ϴ���Ŀ��URL
        /// </summary>
        private string? targetURL;
        private string? writeURL;
        #endregion

        #region Init
        /// <summary>
        /// ��ʼ����Ҫ�����ʵ��,����ȡ���úͰ󶨿ؼ���
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            Init();
            InitWatcher(txtPath.Text);
        }

        /// <summary>
        /// ��ʼ����Ҫ�����ʵ��,����ȡ���úͰ󶨿ؼ���
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            log.Info($"��ʼ��������...");

            try
            {
                #region ��ȡ����
                log.Info($"��ȡ�����ļ�: <LastPath>...");
                var lastPath = Properties.Settings.Default.LastPath;
                if (Directory.Exists(lastPath))
                {
                    txtPath.Text = lastPath;
                    log.Info($"��ȡ�����ļ�: <LastPath> : {lastPath}");
                }
                else
                {
                    MessageBox.Show($"{lastPath} ������, ������ѡ��Ŀ¼!","��ȡ���ó���",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                    log.Warn($"{lastPath} ������, ������ѡ��Ŀ¼!");
                }

                log.Info($"��ȡ�����ļ�: <TargetURL>...");
                targetURL = Properties.Settings.Default.TargetURL;
                log.Info($"��ȡ�����ļ�: <TargetURL> {targetURL}");

                log.Info($"��ȡ�����ļ�: <WriteURL>...");
                writeURL = Properties.Settings.Default.WriteURL;
                log.Info($"��ȡ�����ļ�: <WriteURL> : {writeURL}");

                log.Info($"��ȡ�����ļ�: <AllowedExtensions>...");
                allowedExtensions = Properties.Settings.Default.AllowedExtensions.Split("|");
                log.Info($"��ȡ�����ļ�: <allowedExtensions> : {allowedExtensions}");

                log.Info($"��ȡ�����ļ�: <AllowedFileNameRules>...");
                allowedFileNameRules = Properties.Settings.Default.AllowedFileNameRules;
                log.Info($"��ȡ�����ļ�: <AllowedFileNameRules> : {allowedFileNameRules}");
                #endregion

                #region �ؼ���ʼ��
                log.Info($"��ʼ���ؼ�...");
#if DEBUG
                this.txtPath.ReadOnly = false;
#else
                this.txtPath.ReadOnly = true; // �����汾�н�ֱֹ�����ı������޸�·��,����ͨ��UI�������޸�
#endif

                this.notifyIcon = new NotifyIcon();
                this.contextMenuStrip = new ContextMenuStrip();
                this.menuShow = new ToolStripMenuItem("������");
                this.menuExit = new ToolStripMenuItem("�˳�");

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
                log.Info($"��ʼ���ؼ����!");
                #endregion
            }
            catch(Exception ex)
            {
                log.Error($"��ʼ��������ʧ��: {ex.Message}");
            }
            log.Info($"��ʼ�����������!");
            return true;
        }

        /// <summary>
        /// ��ʼ���ļ������������ָ��·�����ļ��仯�¼���
        /// </summary>
        /// <param name="path">��Ҫ���ӵ�Ŀ¼</param>
        private void InitWatcher(string path)
        {
            // ���֮ǰ�ļ�����
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            // �����µļ�����ʵ��
            watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                // ���ӵ����ݰ����ļ���,���д��ʱ��ʹ���ʱ���
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

        #region UI�¼�
        /// <summary>
        /// ��������ťѡ��һ��Ŀ¼���м�ء�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowsePath_Click(object sender, EventArgs e)
        {
            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = folderBrowserDialog.SelectedPath;
                // ����·����������
                Properties.Settings.Default.LastPath = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.Save();  
                log.Info($"ѡ��·��: {folderBrowserDialog.SelectedPath}");
                InitWatcher(folderBrowserDialog.SelectedPath);                
            }
        }

        /// <summary>
        /// �ϴ���ť����¼�����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnUpload_Click(object sender, EventArgs e)
        {
            #region ����
            // ������ϴ��ļ��б�
            string watchPath = txtPath.Text;
            List<string> pendingFiles = new();
            List<(string?, MouldSizesCutterResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?> 
                responseInfos = new();
            // ��ѯ��ģID���ͺŹ���Ƿ����
            using var queryClient = new HttpClient();
            // �ϴ��ļ�
            using var sendClient = new HttpClient();
            // д���ļ�
            using var writeClient = new HttpClient();
            #endregion

            // ���·���Ƿ����
            log.Info($"�ϴ��¼���ʼ");
            log.Info($"���·���Ƿ����: {watchPath}");
            if (File.Exists(UploadTrackerPaths.PendingPath))
            {
                try
                {
                    var pendingJson = await File.ReadAllTextAsync(UploadTrackerPaths.PendingPath);
                    var state = JsonSerializer.Deserialize<UploadState>(pendingJson);
                    if (state == null || state.FilesToUpload.Count == 0)
                    {
                        log.Warn("û�д��ϴ����ļ��������ϴ�");
                        MessageBox.Show("û�д��ϴ����ļ�");
                        return;
                    }
                    // �ָ��ɾ���·��
                    pendingFiles = state.FilesToUpload.Select(file => Path.Combine(watchPath, file)).ToList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("��ȡ���ϴ��ļ��б�ʧ�ܣ�" + ex.Message);
                    log.Error("��ȡ���ϴ��ļ��б�ʧ��", ex);
                    return;
                }
            }
            
            // �����ͺŲ�ѯ��ģID���ͷ            
            queryClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
            queryClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
            queryClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
            queryClient.DefaultRequestHeaders.Add("X-Timestamp", DateTime.Now.ToString());

            // ��鱾�صĺϷ����Լ��������ϵĺϷ���
            foreach (var file in pendingFiles)
            {
                // ����ļ��Ƿ����
                if (!File.Exists(file))
                {
                    log.Warn($"�ļ� {file} �����ڣ������ϴ�");
                    continue;
                }

                try
                {
                    // ��ȡ�ļ��������,�ļ����а����ͺź͹��
                    string[]? fileNameParts = Path.GetFileNameWithoutExtension(file)?.Split();
                    log.Info($"���ʵ�ַ: {writeURL}?partsCode={fileNameParts?[0]}&specification={fileNameParts?[1]}");

                    // ��getȥ���ͺŹ���Ӧ��ID
                    log.Info($"��ѯ�ͺŹ���Ӧ��ID...");
                    var queryResponse = await queryClient.GetAsync($"{writeURL}?partsCode={fileNameParts?[0]}&specification={fileNameParts?[1]}");
                    var queryResult = await queryResponse.Content.ReadAsStringAsync();
                    if (!queryResponse.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"��ѯʧ�ܣ�{queryResponse.StatusCode}\n{queryResult}");
                        log.Warn($"��ѯʧ�ܣ�{queryResult}");
                        continue;
                    }

                    // get���
                    var queryData = JsonSerializer.Deserialize<MouldSizesCutterResponseModel>(queryResult);
                    log.Info($"��ѯ����,���ڴ���...");
                    
                    // ������е��ͺŹ�����Ѿ����˸ñ���ļ�,�ǾͲ��ϴ���
                    if (queryData?.data.Count != 0 && (queryData?.data.Select(d => d.fileName.Split().Last()).Contains(fileNameParts?[2]) ?? true))
                    {
                        log.Info($"�ļ� {file} �Ѿ��������ͺŹ���У������ϴ�");
                        continue;
                    }
                    // ������µı��
                    responseInfos.Add((file, queryData, null, null));
                }
                catch (Exception)
                {
                    continue;
                }
            }

            // �ԺϷ��ļ������ϴ�
            foreach(var file in responseInfos.Select(responses => responses?.Item1).ToList())
            {
                try
                {
                    // ��ȡ�ļ�����
                    log.Info($"��ȡ�ļ�����...");
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(file));
                    log.Info($"��ȡ�ļ�������ɣ��ļ���С: {fileContent.Headers.ContentLength} �ֽ�");

                    // ��������ͷ
                    fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
                    var form = new MultipartFormDataContent();
                    form.Add(fileContent, "file", Path.GetFileName(file));

                    // �����ϴ��ӿ�                
                    sendClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
                    sendClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
                    sendClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
                    sendClient.DefaultRequestHeaders.Add("X-User-Name", Properties.Settings.Default.XUserName);

                    // ��ȡ��Ӧ������
                    var response = await sendClient.PostAsync(targetURL, form);
                    var result = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        // ��result����FileStorageUploadParamResponseModel������
                        var uploadResponse = JsonSerializer.Deserialize<FileStorageUploadParamResponseModel>(result);

                        if (uploadResponse?.code == "00")
                        {
                            log.Info($"�ϴ��ļ��ɹ�:{result}");

                            // ��Ӧ���������
                            responseInfos[responseInfos.IndexOf(responseInfos.First(response => response?.Item1 == file))] = (file, responseInfos.Last()?.Item2, uploadResponse, null);
                        }
                        else
                        {
                            // ����ϴ�ʧ��,���responseInfos���Ƴ����ļ�
                            responseInfos.Remove(responseInfos.First(response => response?.Item1 == file));

                            MessageBox.Show($"�ϴ��ļ�ʧ��:{response.StatusCode}\n{result}");
                            log.Warn($"�ϴ��ļ�ʧ��:{result}");
                        }
                    }
                    else
                    {
                        // ����ϴ�ʧ��,���responseInfos���Ƴ����ļ�
                        responseInfos.Remove(responseInfos.First(response => response?.Item1 == file));

                        MessageBox.Show($"�ϴ��ļ�ʧ��:{response.StatusCode}\n{result}");
                        log.Warn($"�ϴ��ļ�ʧ��:{result}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("�ϴ�����" + ex.Message);
                    log.Error("�ϴ��쳣", ex);
                }
            }

            // ���ϴ��ɹ����ļ�����д�����
            try
            {                
                if (responseInfos.Count == 0)
                {
                    log.Info($"û���ϴ��ɹ����ļ�!����д��!");
                    MessageBox.Show("û���ϴ��ɹ����ļ�������д��\n������־����...");
                    return;
                }
                // ����д��ӿ�                
                writeClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
                writeClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
                writeClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
                
                // ����д��ӿ���Ҫ������
                var jsonContent = JsonSerializer.Serialize(responseInfos.Select(response => new MouldSizesCutterRequestModel()
                {
                    containerNum = response?.Item2?.data.FirstOrDefault()?.containerNum ?? 0,
                    cutterBlankSpec = (response?.Item2?.data.FirstOrDefault()?.cutterBlankSpec ?? 0).ToString(),
                    cutterType = response?.Item2?.data.FirstOrDefault()?.cutterType ?? 0,
                    fileId = long.Parse(response?.Item3?.data.FirstOrDefault()?.fileId ?? "0"),
                    fileName = response?.Item3?.data.FirstOrDefault()?.fileName ?? string.Empty,
                    fileUrl = response?.Item3?.data.FirstOrDefault()?.fileUrl ?? string.Empty,
                    mouldSizeCutterId = response?.Item2?.data.FirstOrDefault()?.mouldSizeCutterId ?? 0,
                    mouldSizeId = response?.Item2?.data.FirstOrDefault()?.mouldSizeId ?? 0,
                    seq = response?.Item2?.data.FirstOrDefault()?.seq ?? 0
                }), new JsonSerializerOptions { WriteIndented = true });
                log.Info($"{jsonContent}");

                // ��ʼд��
                HttpContent httpContent = new StringContent(jsonContent);
                httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                var response = await writeClient.PostAsync(writeURL, httpContent);
                var result = await response.Content.ReadAsStringAsync();

                // �ɹ�
                if (response.IsSuccessStatusCode)
                {
                    var writeResponse = JsonSerializer.Deserialize<MouldSizesCutterPostResponseModel>(result);
                    if (writeResponse?.code == "00") log.Info($"д�����ݳɹ�: {result}");
                    else
                    {
                        MessageBox.Show($"д������ʧ��: {response.StatusCode}\n{result}");
                        log.Warn($"д������ʧ��: {result}");
                        return;
                    }
                }
                else
                {
                    MessageBox.Show($"д������ʧ��: {response.StatusCode}\n{result}");
                    log.Warn($"д������ʧ��: {result}");
                    return;
                }

                // ����ֻ�гɹ���ִ��
                // ���� uploaded.json �ļ�
                // �� responseInfos �е��ļ�����ӵ� uploaded.json ��
                var uploadedFiles = new HashSet<string>();
                if (File.Exists(UploadTrackerPaths.UploadedPath))
                {
                    try
                    {
                        var uploadedJson = File.ReadAllText(UploadTrackerPaths.UploadedPath);
                        uploadedFiles = JsonSerializer.Deserialize<HashSet<string>>(uploadedJson);
                    }
                    catch
                    {
                        log.Warn("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
                    }
                }

                // �ļ���Ϊ�յ���������ܳ���
                responseInfos.ForEach(response => uploadedFiles?.Add(Path.GetFileName(response?.Item1) ?? ""));

                File.WriteAllText(UploadTrackerPaths.UploadedPath, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info($"���� uploaded.json �ļ��ɹ�������� {uploadedFiles?.Count} ���ļ���");                
            }
            catch (Exception ex)
            {
                log.Error("�����ϴ����ʱ��������", ex);
                MessageBox.Show("�����ϴ����ʱ��������" + ex.Message);
                return;
            }            
        }
        #endregion

        #region �����¼�
        /// <summary>
        /// �ر�ʱ��С��
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // ������û��رմ��ڣ�����С��������
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon.ShowBalloonTip(1000, "��̨������", "��������С��������", ToolTipIcon.Info);
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// �������ͼ��ʱ��ʾ�����ڡ�
        /// </summary>
        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }
        #endregion

        #region ����¼�
        /// <summary>
        /// �ļ�������
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            log.Info($"[FileRenamed] {e.ChangeType}: {e.FullPath}");
            UpdateFileRecord();
        }

        /// <summary>
        /// �ļ������޸�
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
        /// ���´������ļ��б� pending.json �ļ��С�
        /// </summary>
        private void UpdateFileRecord()
        {
            // ���·���Ƿ����
            string watchPath = txtPath.Text;
            if (!Directory.Exists(watchPath)) return;

            // �����׺��������ļ�������
            var currentFiles = Directory
                .EnumerateFiles(watchPath)
                .Where(f => allowedExtensions?.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) ?? false)
                .Select(f => Path.GetFileName(f))
                .Where(name => Regex.Match(name, @allowedFileNameRules).Success)
                .ToList();

            // �������ϴ����ļ��б�
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
            // ��������� uploaded.json �ļ����򴴽�һ���յ�
            else
            {
                uploadedFiles = new();
                File.WriteAllText(UploadTrackerPaths.UploadedPath, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info("�״����У��ѳ�ʼ�� uploaded.json");
            }

            // ɸѡ�����ϴ����ļ�
            var pendingFiles = currentFiles
                .Where(name => !uploadedFiles.Contains(name))
                .ToList();

            // д�� pending.json �ļ�
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
    }
}
