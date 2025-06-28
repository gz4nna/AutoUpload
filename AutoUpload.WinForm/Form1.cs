using AutoUpload.Models;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;
using log4net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoUpload.WinForm
{
    public partial class Form1 : Form
    {
        #region ��Ա����
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
        /// ���ϴ����ļ��б�
        /// </summary>
        private List<string> pendingFiles;
        /// <summary>
        /// �ϴ���Ŀ��URL
        /// </summary>
        private string? targetURL;
        /// <summary>
        /// д���URL
        /// </summary>
        private string? writeURL;
        #endregion

        #region ��ʼ��
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
                    labelPathHint.Text = $"��ǰ���Ŀ¼: {lastPath}";
                }
                else
                {
                    MessageBox.Show($"{lastPath} ������, ������ѡ��Ŀ¼!", "��ȡ���ó���", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                // �ڵ�ǰĿ¼�´���һ��upload�ļ���
                if (!Directory.Exists(UploadTrackerPaths.UploadFolder))
                {
                    Directory.CreateDirectory(UploadTrackerPaths.UploadFolder);
                    log.Info($"�����ϴ��ļ���: {UploadTrackerPaths.UploadFolder}");
                }
                // �������ϴ����ļ��б�
                var uploadedFiles = new HashSet<UploadJsonModel>();

                // ���ļ����ж�ȡȫ���ϴ��ļ�
                if (File.Exists(UploadTrackerPaths.UploadFolder))
                {
                    try
                    {
                        // ÿ���ļ�
                        foreach (var file in Directory.EnumerateFiles(UploadTrackerPaths.UploadFolder))
                        {
                            var uploadedJson = File.ReadAllText(file);
                            var tmpfile = JsonSerializer.Deserialize<List<UploadJsonModel>>(uploadedJson);
                            // ֻҪ���ǿ�
                            if (tmpfile == null || tmpfile.Count == 0) continue;
                            // �����ϴ��ļ��б���
                            tmpfile.ForEach(fileName => uploadedFiles.Add(fileName));
                        }

                        if (listBoxUploadComplete.InvokeRequired)
                        {
                            listBoxUploadComplete.Invoke(new Action(() => listBoxUploadComplete.Items.Clear()));
                            uploadedFiles
                            ?.GroupBy(file => file.uploadTime)?.Last()?.ToList()
                            ?.ForEach(
                                file => listBoxPendingUpload.Invoke(
                                    new Action(() => listBoxPendingUpload.Items.Add(file.fileName))
                                )
                            );
                        }
                        else
                        {
                            listBoxUploadComplete.Items.Clear();
                            uploadedFiles?.ToList()?.ForEach(file => listBoxUploadComplete.Items.Add(file.fileName));
                        }
                    }
                    catch
                    {
                        log.Warn("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
                        labelUploadHintPrint("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
                    }
                }
                #endregion
            }
            catch (Exception ex)
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
            // UpdateFileRecord();
        }
        #endregion

        #region �ؼ��¼�
        /// <summary>
        /// ���ð�ť����¼������л�������ҳ�档
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSetting_Click(object sender, EventArgs e)
        {
            log.Info($"�л�������ҳ��");
            this.tabControl.SelectedTab = this.tabPageSetting;
        }

        /// <summary>
        /// ��������ťѡ��һ��Ŀ¼���м�ء�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowsePath_Click(object sender, EventArgs e)
        {
            log.Info($"�����ť�������ѡ��Ŀ¼...");
            labelPathHint.Text.Remove(0, labelPathHint.Text.Length); // �����ʾ�ı�

            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = folderBrowserDialog.SelectedPath;
                // ����·����������
                Properties.Settings.Default.LastPath = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.Save();
                log.Info($"ѡ��·��: {folderBrowserDialog.SelectedPath}");
                labelPathHint.Text = $"��ǰ���Ŀ¼: {folderBrowserDialog.SelectedPath}";
                InitWatcher(folderBrowserDialog.SelectedPath);
            }
        }

        /// <summary>
        /// ��־��ť����¼������л�����־ҳ�档
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLog_Click(object sender, EventArgs e)
        {
            log.Info($"�л�����־ҳ��");
            this.tabControl.SelectedTab = this.tabPageLog;
            // ��ָ��Ŀ¼
            if (Directory.Exists(UploadTrackerPaths.LogFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", UploadTrackerPaths.LogFolder);
                log.Info($"����־Ŀ¼: {UploadTrackerPaths.LogFolder}");
            }
            else
            {
                //MessageBox.Show($"��־Ŀ¼������: {UploadTrackerPaths.LogFolder}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                log.Error($"��־Ŀ¼������: {UploadTrackerPaths.LogFolder}");
            }
        }

        /// <summary>
        /// �ϴ���ť����¼�����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnUpload_Click(object sender, EventArgs e)
        {
            string watchPath = txtPath.Text;

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
                        //MessageBox.Show("û�д��ϴ����ļ�");
                        labelUploadHintPrint("û�д��ϴ����ļ�");
                        return;
                    }

                    // �ָ��ɾ���·��
                    pendingFiles = state.FilesToUpload.Select(file => Path.Combine(watchPath, file)).ToList();
                    // ��ҳ
                    this.tabControl.SelectedTab = this.tabPageUpload;
                }
                catch (Exception ex)
                {
#if DEBUG
                    MessageBox.Show("��ȡ���ϴ��ļ��б�ʧ�ܣ�" + ex.Message);
#elif RELEASE
                    labelUploadHintPrint("��ȡ���ϴ��ļ��б�ʧ��");
#endif
                    log.Error("��ȡ���ϴ��ļ��б�ʧ��", ex);
                    // ��ҳ
                    this.tabControl.SelectedTab = this.tabPageUpload;
                    return;
                }
            }
#if DEBUG
            //return;
#endif
            await UploadPre();
        }

        /// <summary>
        /// �ϴ�ǰ��Ԥ������������ Upload ���������ļ��ϴ�������
        /// </summary>
        /// <returns></returns>
        private async Task UploadPre() => await Upload();

        /// <summary>
        /// �˳���ť����¼������ر�Ӧ�ó���
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
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
            UpdateFileRecord(e.FullPath);
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
            UpdateFileRecord(e.FullPath);
        }

        /// <summary>
        /// �����ļ���¼�����������ļ��仯�¼������´��ϴ��ļ��б�
        /// </summary>
        /// <param name="changedFilePath"></param>
        private async void UpdateFileRecord(string changedFilePath = "")
        {
            log.Info($"UpdateFileRecord");
            // ���·���Ƿ����
            string watchPath = txtPath.Text;            
            var currentFiles = Directory.EnumerateFiles(watchPath)
                .Select(file=> Path.GetFileName(file))
                .Where(file=> allowedExtensions?
                    .Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ?? false &&
                    Regex.Match(Path.GetFileName(file), @allowedFileNameRules).Success)
                .ToList();

            log.Info("�������д��ϴ�");
            // �ȷ������д��ϴ�
            if (!File.Exists(UploadTrackerPaths.PendingPath))
            {
                log.Warn($"û���ҵ����ϴ��ļ��б�: {UploadTrackerPaths.PendingPath}");
                // ��������ھʹ���һ���µ�
                File.WriteAllText(
                    UploadTrackerPaths.PendingPath, 
                    JsonSerializer.Serialize(new UploadState
                        {
                            AllFilesInFolder = currentFiles,
                            FilesToUpload = new List<string>()
                        }, new JsonSerializerOptions { WriteIndented = true }
                ));
                log.Info($"�����µĴ��ϴ��ļ��б�: {UploadTrackerPaths.PendingPath}");
            }
            pendingFiles = JsonSerializer.Deserialize<UploadState>
                (File.ReadAllText(UploadTrackerPaths.PendingPath))?.FilesToUpload
                .Where(file=>currentFiles.Contains(file))
                .ToList() ?? new();

            if (watchPath.Length > 0)
            {
                if (!File.Exists(changedFilePath)) return;
                if (allowedExtensions?
                    .Contains(Path.GetExtension(changedFilePath), StringComparer.OrdinalIgnoreCase) ?? false &&
                    Regex.Match(Path.GetFileName(changedFilePath), @allowedFileNameRules).Success)
                    // ����ļ������Ϲ���
                    pendingFiles.Add(Path.GetFileName(changedFilePath));
            }

            log.Info("���б������ʾ���ϴ��ļ��б�");
            // ���б������ʾ���ϴ��ļ��б�
            if (listBoxPendingUpload.InvokeRequired)
            {
                listBoxPendingUpload.Invoke(new Action(() => listBoxPendingUpload.Items.Clear()));
                pendingFiles.ForEach(
                    file => listBoxPendingUpload.Invoke(
                        new Action(() => listBoxPendingUpload.Items.Add(file))
                    )
                );
            }
            else
            {
                listBoxPendingUpload.Items.Clear();
                pendingFiles.ForEach(
                    file => listBoxPendingUpload.Items.Add(file)
                );
            }

            log.Info("���� pending.json �ļ�");
            // д�� pending.json �ļ�
            var state = new UploadState
            {
                AllFilesInFolder = currentFiles,
                FilesToUpload = pendingFiles
            };
            var pendingJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UploadTrackerPaths.PendingPath, pendingJson);

            log.Info($"��ظ��£����ļ� {currentFiles.Count} �������ϴ� {pendingFiles.Count} ��");
            await UploadPre();
        }
        #endregion

        #region �ϴ��߼�
        /// <summary>
        /// �ϴ������������ļ��ϴ��߼���
        /// </summary>
        /// <returns></returns>
        private async Task Upload()
        {
            if (pendingFiles == null || pendingFiles.Count == 0) return;
            #region ����
            // ������ϴ��ļ��б�
            string watchPath = txtPath.Text;
            List<(string?, MouldSizesCutterResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>
                responseInfos = new();
            // ��ѯ��ģID���ͺŹ���Ƿ����
            using var queryClient = new HttpClient();
            // �ϴ��ļ�
            using var sendClient = new HttpClient();
            // д���ļ�
            using var writeClient = new HttpClient();
            #endregion

            // �����ͺŲ�ѯ��ģID���ͷ            
            queryClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
            queryClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
            queryClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
            queryClient.DefaultRequestHeaders.Add("X-Timestamp", DateTime.Now.ToString());

            // ��鱾�صĺϷ����Լ��������ϵĺϷ���
            foreach (var file in pendingFiles)
            {
                // ����ļ��Ƿ����
                if (!File.Exists(Path.Combine(watchPath, file)))
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
                        //MessageBox.Show($"��ѯʧ�ܣ�{queryResponse.StatusCode}\n{queryResult}");
                        labelUploadHintPrint($"��ѯʧ��: {queryResponse.StatusCode}");
                        log.Warn($"��ѯʧ�ܣ�{queryResponse.StatusCode}\n{queryResult}");
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
                catch (Exception ex)
                {
                    log.Error(ex);
                    continue;
                }
            }

            // �ԺϷ��ļ������ϴ�
            foreach (var file in responseInfos.Select(responses => responses?.Item1).ToList())
            {
                try
                {
                    // ��ȡ�ļ�����
                    log.Info($"��ȡ�ļ�����...");
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(Path.Combine(watchPath, file)));
                    log.Info($"��ȡ�ļ�������ɣ��ļ���С: {fileContent.Headers.ContentLength} �ֽ�");

                    // ��������ͷ
                    fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
                    var form = new MultipartFormDataContent();
                    form.Add(fileContent, "files", Path.GetFileName(file));

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
                        log.Info($"�ϴ��ļ����:{response}{result}");
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

                            //MessageBox.Show($"�ϴ��ļ�ʧ��:{response.StatusCode}\n{result}");
                            labelUploadHintPrint($"�ϴ��ļ�ʧ��: {response.StatusCode}");
                            log.Warn($"�ϴ��ļ�ʧ��:{result}");
                        }
                    }
                    else
                    {
                        // ����ϴ�ʧ��,���responseInfos���Ƴ����ļ�
                        responseInfos.Remove(responseInfos.First(response => response?.Item1 == file));

                        //MessageBox.Show($"�ϴ��ļ�ʧ��:{response.StatusCode}\n{result}");
                        labelUploadHintPrint($"�ϴ��ļ�ʧ��: {response.StatusCode}");
                        log.Warn($"�ϴ��ļ�ʧ��:{result}");
                    }
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("�ϴ�����" + ex.Message);
                    labelUploadHintPrint("�ϴ��쳣");
                    log.Error("�ϴ��쳣", ex);
                }
            }

            // ���ϴ��ɹ����ļ�����д�����
            try
            {
                if (responseInfos.Count == 0)
                {
                    log.Info($"û���ϴ��ɹ����ļ�!����д��!");
                    //MessageBox.Show("û���ϴ��ɹ����ļ�������д��\n������־����...");
                    labelUploadHintPrint("û���ϴ��ɹ����ļ�������д��\n������־����...");
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
                if (!response.IsSuccessStatusCode)
                {
                    //MessageBox.Show($"д������ʧ��: {response.StatusCode}\n{result}");
                    labelUploadHintPrint($"д������ʧ��: {response.StatusCode}");
                    log.Warn($"д������ʧ��: {result}");
                    return;                    
                }

                var writeResponse = JsonSerializer.Deserialize<MouldSizesCutterPostResponseModel>(result);
                if (writeResponse?.code != "00") 
                {
                    //MessageBox.Show($"д������ʧ��: {response.StatusCode}\n{result}");
                    labelUploadHintPrint($"д������ʧ��: {response.StatusCode}");
                    log.Warn($"д������ʧ��: {response.StatusCode}\n{result}");
                    return;
                }

                log.Info($"д�����ݳɹ�: {result}");

                // ����ֻ�гɹ���ִ��
                // ���� uploaded.json �ļ�
                // �� responseInfos �е��ļ�����ӵ� uploaded.json ��
                var uploadedFiles = new List<UploadJsonModel>();

                if (!Directory.Exists(UploadTrackerPaths.UploadFolder))
                {
                    Directory.CreateDirectory(UploadTrackerPaths.UploadFolder);
                    log.Info($"�����ϴ���¼�ļ���: {UploadTrackerPaths.UploadFolder}");
                }

                // ��Ҫ��,��Ҫ���ϴ���¼�����·ݱ���
                // ���յ�ǰ�ϴ����·�ȥѡ�ļ�
                var monthlyRecord = Path.Combine(UploadTrackerPaths.UploadFolder, $"UploadRecords_{DateTime.Now.ToString("yyyyMM")}.json");
                if (!File.Exists(monthlyRecord))
                {
                    // ��������ھʹ���һ���µ�
                    File.WriteAllText(monthlyRecord, "[]");
                    log.Info($"�����µ��ϴ���¼�ļ�: {monthlyRecord}");
                }

                try
                {
                    // ��������µ��ϴ���¼
                    var uploadedJson = File.ReadAllText(monthlyRecord);
                    // �����л�Ϊ List<UploadJsonModel>
                    uploadedFiles = JsonSerializer.Deserialize<List<UploadJsonModel>>(uploadedJson);
                }
                catch
                {
                    log.Warn("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
                }

                // �������ϴ��ɹ��ļ�¼,�ļ���Ϊ�յ���������ܳ���
                responseInfos.ForEach(
                    response => uploadedFiles?.Add(
                        new()
                        {
                            fileName = Path.GetFileName(response?.Item1) ?? "",
                            uploadTime = response?.Item3?.timestamp ?? DateTime.Now.ToString()
                        }
                    )
                );
                
                // ���б������ʾ���ϴ��ļ��б�
                if (listBoxUploadComplete.InvokeRequired)
                {
                    listBoxUploadComplete.Invoke(new Action(() => listBoxUploadComplete.Items.Clear()));
                    uploadedFiles?.ForEach(
                        file => listBoxUploadComplete.Invoke(
                            new Action(() => listBoxUploadComplete.Items.Add(Path.GetFileName(file.fileName) ?? string.Empty))
                        )
                    );
                }
                else
                {
                    listBoxUploadComplete.Items.Clear();
                    uploadedFiles?.ForEach(
                        file => listBoxUploadComplete.Items.Add(Path.GetFileName(file.fileName) ?? string.Empty)
                    );
                }

                // ���б����ɾ�����ϴ��ļ��б�
                if (listBoxPendingUpload.InvokeRequired)
                {
                    responseInfos
                        .Where(file => file?.Item1 != null).ToList()
                        .ForEach(file => listBoxPendingUpload.Invoke(
                            new Action(() => listBoxPendingUpload.Items.Remove(Path.GetFileName(file?.Item1)))
                        )
                    );
                }
                else
                {
                    responseInfos
                        .Where(file => file?.Item1 != null).ToList()
                        .ForEach(file => listBoxPendingUpload.Items.Remove(Path.GetFileName(file?.Item1))
                    );
                }

                // ɾ�� pending.json ���ϴ��ɹ����ļ�
                var pendingState = JsonSerializer.Deserialize<UploadState>(File.ReadAllText(UploadTrackerPaths.PendingPath));
                if (pendingState != null) {
                    pendingState.FilesToUpload = pendingState.FilesToUpload
                        .Where(file => !responseInfos.Select(res => res?.Item1).Contains(file))
                        .ToList();
                    // д�� pending.json
                    File.WriteAllText(UploadTrackerPaths.PendingPath, JsonSerializer.Serialize(pendingState, new JsonSerializerOptions { WriteIndented = true }));
                }

                // д�����ϴ��ļ�
                File.WriteAllText(monthlyRecord, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info($"���� uploaded.json �ļ��ɹ�������� {uploadedFiles?.Count} ���ļ���");
                labelUploadHintPrint($"�ϴ��ɹ�������� {uploadedFiles?.Count} ���ļ�,{listBoxPendingUpload.Items.Count}���ϴ�ʧ��");
            }
            catch (Exception ex)
            {
                log.Error("�����ϴ����ʱ��������", ex);
                //MessageBox.Show("�����ϴ����ʱ��������" + ex.Message);
                labelUploadHintPrint("�����ϴ����ʱ��������");
                return;
            }
        }

        private void labelUploadHintPrint(string hint)
        {
            if (labelUploadHint.InvokeRequired) labelUploadHint.Invoke(new Action(() => labelUploadHint.Text = hint));
            else labelUploadHint.Text = hint;
        }
        #endregion
    }
}
