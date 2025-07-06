using System.Text.Json;
using System.Text.RegularExpressions;

using AutoUpload.Models;
using AutoUpload.Models.Helpers;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;

using log4net;

namespace AutoUpload.WinForm
{
    public partial class Form1 : Form, IDisposable
    {
        #region ��Ա����
        // ��־
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));
        // ÿСʱִ��һ�ε� System.Threading.Timer ��ʱ��
        private System.Threading.Timer? timer;

        // �ͷ���Դ���
        private bool disposed = false;

        // ��settings����ȡ��
        /// <summary>
        /// �ϴ��ļ��ĺ�׺������
        /// </summary>
        private string[]? allowedExtensions;
        /// <summary>
        /// �ļ����Ϸ�����
        /// </summary>
        private string? allowedFileNameRules;

        // ˽�г�Ա����
        /// <summary>
        /// �����ļ��м�����
        /// </summary>
        private DelayFileSystemWatcherHelper? watcher;
        /// <summary>
        /// ���ϴ����ļ��б�
        /// </summary>
        private List<string>? pendingFiles;
        /// <summary>
        /// �ϴ���Ŀ��URL
        /// </summary>
        private string? targetURL;
        /// <summary>
        /// д���URL
        /// </summary>
        private string? writeURL;
        /// <summary>
        /// ���ݾ��ͺŲ������ͺŵĽӿڵ�ַ
        /// </summary>
        private string? queryURL;
        /// <summary>
        /// ��һ���ϴ���ʱ��������ѯ mouldSizeId �Ľӿڵ�ַ
        /// </summary>
        private string? listURL;
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

                log.Info($"��ȡ�����ļ�: <QueryURL>...");
                queryURL = Properties.Settings.Default.QueryURL;
                log.Info($"��ȡ�����ļ�: <QueryURL> : {queryURL}");

                log.Info($"��ȡ�����ļ�: <ListURL>...");
                listURL = Properties.Settings.Default.ListURL;
                log.Info($"��ȡ�����ļ�: <ListURL> : {listURL}");

                log.Info($"��ȡ�����ļ�: <AllowedExtensions>...");
                allowedExtensions = Properties.Settings.Default.AllowedExtensions.Split("|");
                log.Info($"��ȡ�����ļ�: <allowedExtensions> : {Properties.Settings.Default.AllowedExtensions}");

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

                timer = new System.Threading.Timer(
                    async _ => await UploadPre(),
                    null,
                    TimeSpan.Zero, // ����ִ��
                    TimeSpan.FromHours(1) // ÿСʱִ��һ��
                );
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
                if (Directory.Exists(UploadTrackerPaths.UploadFolder))
                {
                    try
                    {
                        // ÿ���ļ�
                        foreach (var file in Directory.EnumerateFiles(UploadTrackerPaths.UploadFolder))
                        {
                            var fileName = Path.GetFileName(file);
                            // ���������µ�
                            // UploadRecords_202506.json
                            if (!fileName.Equals($"UploadRecords_{DateTime.Now.ToString("yyyyMM")}.json")) continue;

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
            watcher?.Dispose();

            // �����µļ�����ʵ��
            watcher = new DelayFileSystemWatcherHelper(path, "*.*", 800);

            watcher.DelayChanged += (changedFile) =>
            {
                // ��������ʱ�ϲ���Ļص�
                this.Invoke(new Action(() => UpdateFileRecord(changedFile ?? "")));
            };
            watcher.Start();
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
            try
            {
                Dispose();
                Application.Exit();
            }
            catch(Exception ex)
            {
                log.Error($"�˳�����ʱ��������!", ex);
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
            log.Info($"UpdateFileRecord: {changedFilePath}");
            // ���·���Ƿ����
            string watchPath = txtPath.Text;
            var currentFiles = Directory.EnumerateFiles(watchPath)
                .Select(file => Path.GetFileName(file))
                .Where(file => allowedExtensions?
                    .Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ?? false &&
                    Regex.Match(Path.GetFileName(file), @allowedFileNameRules).Success)
                .ToList();
            log.Info($"�Ѽ��� {watchPath} �������ļ�,�� {currentFiles.Count} �����Ϲ�����ļ�");

            // �ȷ������д��ϴ�
            log.Info($"��ȡ���ϴ��ļ��б�: {UploadTrackerPaths.PendingPath}");
            // ������ʱ����һ���µ�
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
            log.Info($"ȥ����ǰĿ¼���Ѳ����ڵ��ļ�");
            // ���ڴ��ϴ������ڵ�ǰĿ¼�����ҵ���Щ
            pendingFiles = JsonSerializer.Deserialize<UploadState>
                (File.ReadAllText(UploadTrackerPaths.PendingPath))?.FilesToUpload
                .Where(file => currentFiles.Contains(file))
                .ToHashSet()
                .ToList() ?? new();

            var changedFilePathSplits = changedFilePath.Split("|");
            foreach (var filePath in changedFilePathSplits)
            {
                log.Info($"�����ļ��仯: {filePath}");
                // �ų���������ʱΪɾ���ļ������
                if (!File.Exists(filePath))
                {
                    // �Ӵ��ϴ��б����Ƴ�
                    if (pendingFiles.Contains(Path.GetFileName(filePath))) pendingFiles.Remove(Path.GetFileName(filePath));
                    return;
                }

                // ����ļ�֮ǰ�ж��Ƿ���Ϻ�׺�������������
                if (allowedExtensions?
                    .Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase) ?? false &&
                    // �������ƹ���
                    Regex.Match(Path.GetFileName(filePath), @allowedFileNameRules).Success)
                    pendingFiles.Add(Path.GetFileName(filePath));
                // ȥ��
                pendingFiles = pendingFiles.ToHashSet().ToList();
                log.Info($"������޸��ļ�: {Path.GetFileName(filePath)}");
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

            log.Info($"���� {UploadTrackerPaths.PendingPath} �ļ�");
            // д�� pending.json �ļ�,��ʧЧ�Ĳ��ᱻд��
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
            List<(string?,
                MouldSizesCutterResponseModel?,
                MouldSizesListResponseModel?,
                FileStorageUploadParamResponseModel?,
                MouldSizesCutterRequestModel?)?>
                responseInfos = new();
            using var httpClient = new HttpClient();
            var newPartsCode = "";
            #endregion

            // �����ͺŲ�ѯ��ģID���ͷ            
            httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
            httpClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
            httpClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
            httpClient.DefaultRequestHeaders.Add("X-User-Name", Properties.Settings.Default.XUserName);
            httpClient.DefaultRequestHeaders.Add("X-Timestamp", DateTime.Now.ToString());

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

                    #region ��ȡ���ͺ�
                    // Ϊ��ֹʹ��Эͬ�в����ڵľ��ͺ�,���ȳ��Ի�ȡ��Ӧ�����ͺ�
                    log.Info($"���Ի�ȡ��Ӧ�����ͺ�...");
                    log.Info($"���ʵ�ַ: {queryURL}?mbomCode={fileNameParts?[0]}");
                    var partsAttrValueQueryResponse = await httpClient.PostAsync(
                        $"{queryURL}?mbomCode={fileNameParts?[0]}",
                        new StringContent(string.Empty));
                    var partsAttrValueQueryResult = await partsAttrValueQueryResponse.Content.ReadAsStringAsync();

                    if (!partsAttrValueQueryResponse.IsSuccessStatusCode)
                    {
                        log.Info($"��ѯ���ͺŹ��ʧ��,�����������ͺţ�{partsAttrValueQueryResponse.StatusCode}\n{partsAttrValueQueryResult}");
                        newPartsCode = fileNameParts?[0];
                    }
                    else
                    {
                        // �����ѯ�ɹ�,��������µ��ͺ�
                        var partsAttrValueQueryData = JsonSerializer.Deserialize<PartsAttrValuePostResponseModel>(partsAttrValueQueryResult);
                        newPartsCode = partsAttrValueQueryData?.data?.First()?.partsCode ?? fileNameParts?[0];
                    }
                    #endregion

                    #region ��ѯ�����б�
                    // ��getȥ���ͺŹ���Ӧ��ID
                    log.Info($"��ѯ�ͺŹ���Ӧ��ID...");
                    log.Info($"���ʵ�ַ: {writeURL}?partsCode={newPartsCode}&specification={fileNameParts?[1]}");
                    // ���û�����ͺ�,��ʹ��ԭ�����ͺ�
                    var queryResponse = await httpClient.GetAsync(
                        $"{writeURL}?partsCode={newPartsCode}&specification={fileNameParts?[1]}"
                    );
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

                    if (queryData?.code != "00")
                    {
                        log.Warn($"��ѯ�ͺŹ��ʧ��: {queryData?.error} {queryData?.message}");
                        continue;
                    }

                    // ������е��ͺŹ�����Ѿ����˸ñ���ļ�,�ǾͲ��ϴ���
                    if (
                        // count Ϊ0������ֱ���ϴ�,ֻ���õ���������ʱ��Ҫ�ж�
                        queryData?.data?.Count != 0 &&
                        // ֻ�й������:һ�����,Ҫ�� fileNameParts?.Length != 2 ����������
                        fileNameParts?.Length != 2 &&
                        // �б�ŵ����:���һ��part�϶��Ǳ��,����ͬ�ı�žͲ��ϴ���
                        (queryData?.data?.Select(d => d?.fileName?.Split().Last()).Contains(fileNameParts?.Last()) ?? true))
                    {
                        log.Info($"�ļ� {file} �Ѿ��������ͺŹ���У������ϴ�");
                        continue;
                    }
                    #endregion

                    #region  ��ȡ��ģ���
                    // ��ȡ��ģ���
                    log.Info($"��ȡ��ģ���...");
                    log.Info($"���ʵ�ַ: {listURL}?current=1&partsCode={newPartsCode}&size=15");
                    var listResponse = await httpClient.GetAsync($"{listURL}?current=1&partsCode={newPartsCode}&size=15");
                    var listResult = await listResponse.Content.ReadAsStringAsync();
                    if (!listResponse.IsSuccessStatusCode)
                    {
                        //MessageBox.Show($"��ȡ��ģ���ʧ�ܣ�{listResponse.StatusCode}\n{listResult}");
                        labelUploadHintPrint($"��ȡ��ģ���ʧ��: {listResponse.StatusCode}");
                        log.Warn($"��ȡ��ģ���ʧ�ܣ�{listResponse.StatusCode}\n{listResult}");
                        // ��ȡʧ�ܾ��ò�������������ȥ,����ֱ�������Ϳ���
                        continue;
                    }
                    // ��ȡ���Ľ��
                    var listData = JsonSerializer.Deserialize<MouldSizesListResponseModel>(listResult);
                    log.Info($"��ȡ��ģ��Ž���,���ڴ���...");
                    if (listData?.code != "00")
                    {
                        log.Warn($"��ȡ��ģ���ʧ��: {listData?.errors} {listData?.message}");
                        continue;
                    }
                    #endregion

                    // ������µı�ż�����׼���ϴ�
                    // file ����ԭ�����ļ���, queryData �������ͺŵ��ļ���
                    responseInfos.Add((file, queryData, listData, null, null));
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
                    if (file == null) continue;
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(Path.Combine(watchPath, file)));
                    log.Info($"��ȡ�ļ�������ɣ��ļ���С: {fileContent.Headers.ContentLength} �ֽ�");

                    // ��������ͷ
                    fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
                    var form = new MultipartFormDataContent();

                    // �ļ������ļ����ݷֿ�,����Ǿ��ͺ����ϴ���ʱ���滻�ɵ�һ���ӿڲ���������ͺ�
                    // ����,ǰ��������,���ﲻ�ж�Ҳû��,ֱ����newPartsCode����
                    if (responseInfos.First(response => response?.Item1 == file)?.Item2?.code != "00")
                    {
                        // ���û�в鵽���ݲ��ҿ����ϴ�,��ֱ��ʹ��ԭ�ļ���
                        form.Add(fileContent, "files", Path.GetFileName(file));
                    }
                    // 00 ��ʾ�ɹ�
                    else
                    {
                        // ����鵽������,ֱ��ʹ�����ͺŵ��ļ���
                        form.Add(
                            fileContent,
                            "files",
                            Path.GetFileName(file).Replace(
                                Path.GetFileName(file).Split().First(),
                                newPartsCode
                            )
                        );
                    }

                    // �����ϴ��ӿ�                                    

                    // ��ȡ��Ӧ������
                    var response = await httpClient.PostAsync(targetURL, form);
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
                            responseInfos[
                                responseInfos.IndexOf(
                                    responseInfos.First(response => response?.Item1 == file)
                                )
                            ] = (file, responseInfos.Last()?.Item2, responseInfos.Last()?.Item3, uploadResponse, null);
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
            // �ϴ�ʧ�ܵĻᱻ�Ƴ�,д��ʱ�ܿ����Ķ����ϴ��ɹ���
            try
            {
                // �������Ķ����ϴ��ɹ����ļ�
                if (responseInfos.Count == 0)
                {
                    log.Info($"û���ϴ��ɹ����ļ�!����д��!");
                    //MessageBox.Show("û���ϴ��ɹ����ļ�������д��\n������־����...");
                    labelUploadHintPrint("û���ϴ��ɹ����ļ�������д��\n������־����...");
                    return;
                }

                #region ����¼�ļ�����
                // ���� uploaded.json �ļ�
                // �� responseInfos �е��ļ�����ӵ� uploaded.json ��
                var uploadedFiles = new List<UploadJsonModel>();

                if (!Directory.Exists(UploadTrackerPaths.UploadFolder))
                {
                    Directory.CreateDirectory(UploadTrackerPaths.UploadFolder);
                    log.Info($"�����ϴ���¼�ļ���: {UploadTrackerPaths.UploadFolder}");
                }

                var pendingState = JsonSerializer.Deserialize<UploadState>(File.ReadAllText(UploadTrackerPaths.PendingPath));

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
                #endregion

                #region д�벢��¼
                // ����д��ӿ�                

                // �����ļ��ֿ�д��,����һ����ȫ��ʧ��
                foreach (var responseInfo in responseInfos)
                {
                    // ����д��ӿ���Ҫ������
                    var jsonContent = JsonSerializer.Serialize(new MouldSizesCutterRequestModel()
                    {
                        containerNum = responseInfo?.Item2?.data?.FirstOrDefault()?.containerNum ?? 0,
                        cutterBlankSpec = (responseInfo?.Item2?.data?.FirstOrDefault()?.cutterBlankSpec ?? 0).ToString(),
                        cutterType = responseInfo?.Item2?.data?.FirstOrDefault()?.cutterType ?? 0,
                        fileId = long.Parse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId ?? "0"),
                        // ȡ����ʱ�򾡿����ÿ���Ľӿڵ���Ӧ,����м����޸����Ǵ���
                        fileName = responseInfo?.Item4?.data?.FirstOrDefault()?.fileName ?? string.Empty,
                        fileUrl = responseInfo?.Item4?.data?.FirstOrDefault()?.fileUrl ?? string.Empty,
                        mouldSizeCutterId = long.Parse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId ?? ""),
                        // ��ģ��Ŵӵ�ģ����б��ѯ�ӿ��л�ȡ
                        mouldSizeId = long.Parse(responseInfo?.Item3?.data?.records?.First(record => record?.specification?.Split('.')?[0] == responseInfo?.Item1?.Split()?[1])?.mouldSizeId ?? "0"),
                        seq = responseInfo?.Item2?.data?.FirstOrDefault()?.seq ?? 0
                    }, new JsonSerializerOptions { WriteIndented = true });
                    log.Info($"{jsonContent}");

                    // ��ʼд��
                    HttpContent httpContent = new StringContent(jsonContent);
                    httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                    var response = await httpClient.PostAsync(writeURL, httpContent);
                    var result = await response.Content.ReadAsStringAsync();

                    // д�벻�ɹ�
                    if (!response.IsSuccessStatusCode)
                    {
                        labelUploadHintPrint($"д������ʧ��: {response.StatusCode}");
                        log.Warn($"д������ʧ��: {result}");
                        // ʧ�ܺ�����һ��
                        continue;
                    }

                    var writeResponse = JsonSerializer.Deserialize<MouldSizesCutterPostResponseModel>(result);
                    if (writeResponse?.code != "00")
                    {
                        labelUploadHintPrint($"д������ʧ��: {response.StatusCode}");
                        log.Warn($"д������ʧ��: {response.StatusCode}\n{result}");
                        // ʧ�ܺ�����һ��
                        continue;
                    }

                    log.Info($"д�����ݳɹ�: {result}");

                    // �������ϴ��ɹ��ļ�¼,�ļ���Ϊ�յ���������ܳ���
                    uploadedFiles?.Add(new()
                    {
                        // ����ʹ��ԭ��
                        fileName = Path.GetFileName(responseInfo?.Item1) ?? "",
                        uploadTime = responseInfo?.Item4?.timestamp ?? DateTime.Now.ToString()
                    });

                    // ɾ�� pending.json ���ϴ��ɹ����ļ�                
                    pendingState?.FilesToUpload.Remove(Path.GetFileName(responseInfo?.Item1) ?? string.Empty);
                }
                #endregion

                #region �����б����ļ�
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

                // д�� pending.json
                File.WriteAllText(UploadTrackerPaths.PendingPath, JsonSerializer.Serialize(pendingState, new JsonSerializerOptions { WriteIndented = true }));
                // д�����ϴ��ļ�
                File.WriteAllText(monthlyRecord, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info($"���� uploaded.json �ļ��ɹ�������� {uploadedFiles?.Count} ���ļ���");
                labelUploadHintPrint($"�ϴ��ɹ�������� {uploadedFiles?.Count} ���ļ�,{listBoxPendingUpload.Items.Count}���ϴ�ʧ��");
                #endregion
            }
            catch (Exception ex)
            {
                log.Error("�����ϴ����ʱ��������", ex);
                //MessageBox.Show("�����ϴ����ʱ��������" + ex.Message);
                labelUploadHintPrint("�����ϴ����ʱ��������");
                return;
            }
        }

        /// <summary>
        /// ���ϴ��������ʾ��ǩ����ʾ���������
        /// </summary>
        /// <param name="hint"></param>
        private void labelUploadHintPrint(string hint)
        {
            if (labelUploadHint.InvokeRequired) labelUploadHint.Invoke(new Action(() => labelUploadHint.Text = hint));
            else labelUploadHint.Text = hint;
        }
        #endregion
    }
}
