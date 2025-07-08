using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

using AutoUpload.Models;
using AutoUpload.Models.Helpers;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;

using log4net;

namespace AutoUpload.WinForm;

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
    private ConcurrentBag<string>? pendingFiles = new();
    private readonly object _lockObj = new();
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
                        listBoxUploadComplete.Invoke(new Action(() =>
                        {
                            listBoxUploadComplete.BeginUpdate();
                            listBoxUploadComplete.SuspendLayout();
                            uploadedFiles
                            ?.GroupBy(file => file.uploadTime)
                            ?.Last()
                            ?.ToList()
                            ?.ForEach(file => listBoxUploadComplete.Items.Add(file.fileName));
                            listBoxUploadComplete.ResumeLayout();
                            listBoxUploadComplete.EndUpdate();
                        }));
                    }
                    else
                    {
                        listBoxUploadComplete.BeginUpdate();
                        listBoxUploadComplete.SuspendLayout();
                        uploadedFiles
                        ?.GroupBy(file => file.uploadTime)
                        ?.Last()
                        ?.ToList()
                        ?.ForEach(file => listBoxUploadComplete.Items.Add(file.fileName));
                        listBoxUploadComplete.ResumeLayout();
                        listBoxUploadComplete.EndUpdate();
                    }
                }
                catch
                {
                    log.Warn("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
                    LabelUploadHintPrint("uploaded.json ��ȡʧ�ܣ�������Ϊ��");
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
                    LabelUploadHintPrint("û�д��ϴ����ļ�");
                    return;
                }

                // �ָ��ɾ���·��
                lock (_lockObj)
                {
                    // ��յ�ǰ����
                    pendingFiles?.Clear();
                    state.FilesToUpload.ForEach(file => pendingFiles?.Add(Path.Combine(watchPath, file)));
                }

                // ��ҳ
                this.tabControl.SelectedTab = this.tabPageUpload;
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show("��ȡ���ϴ��ļ��б�ʧ�ܣ�" + ex.Message);
#elif RELEASE
                LabelUploadHintPrint("��ȡ���ϴ��ļ��б�ʧ��");
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
        catch (Exception ex)
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
            await File.WriteAllTextAsync(
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
        lock (_lockObj)
        {
            pendingFiles?.Clear();
            (JsonSerializer.Deserialize<UploadState>(File.ReadAllText(UploadTrackerPaths.PendingPath))?.FilesToUpload
            .Where(file => currentFiles.Contains(file))
            .ToHashSet()
            .ToList() ?? new())
                .ForEach(file => pendingFiles?.Add(file));
        }

        var changedFilePathSplits = changedFilePath.Split("|");
        foreach (var filePath in changedFilePathSplits)
        {
            log.Info($"�����ļ��仯: {filePath}");
            // �ų���������ʱΪɾ���ļ������
            if (!File.Exists(filePath))
            {
                // �Ӵ��ϴ��б����Ƴ�
                lock (_lockObj)
                {
                    var fileName = Path.GetFileName(filePath);
                    var tempList = pendingFiles?.ToList();
                    pendingFiles?.Clear();
                    tempList?.Where(file => file != fileName).ToList()
                        .ForEach(file => pendingFiles?.Add(file));
                }
                return;
            }

            // ����ļ�֮ǰ�ж��Ƿ���Ϻ�׺�������������
            if (allowedExtensions?
                .Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase) ?? false &&
                // �������ƹ���
                Regex.Match(Path.GetFileName(filePath), @allowedFileNameRules).Success)
            {
                lock (_lockObj)
                {
                    // ����ļ�֮ǰ��ȥ��
                    if (!pendingFiles?.Contains(Path.GetFileName(filePath)) ?? true)
                    {
                        pendingFiles?.Add(Path.GetFileName(filePath));
                    }
                }
            }

            log.Info($"������޸��ļ�: {Path.GetFileName(filePath)}");
        }

        log.Info("���б������ʾ���ϴ��ļ��б�");
        // ʹ����ʱ�б���ⳤʱ��ռ����
        List<string> filesToProcess;
        lock (_lockObj)
        {
            filesToProcess = pendingFiles?.ToList() ?? [];
        }
        if (filesToProcess?.Count != 0)
        {
            // ���б������ʾ���ϴ��ļ��б�
            if (listBoxPendingUpload.InvokeRequired)
            {
                listBoxPendingUpload.Invoke(new Action(() =>
                {
                    listBoxPendingUpload.BeginUpdate();
                    listBoxPendingUpload.SuspendLayout();
                    listBoxPendingUpload.Items.Clear();
                    filesToProcess?.ForEach(file => listBoxPendingUpload.Items.Add(file));
                    listBoxPendingUpload.ResumeLayout();
                    listBoxPendingUpload.EndUpdate();
                }));
            }
            else
            {
                listBoxPendingUpload.BeginUpdate();
                listBoxPendingUpload.SuspendLayout();
                listBoxPendingUpload.Items.Clear();
                filesToProcess?.ForEach(file => listBoxPendingUpload.Items.Add(file));
                listBoxPendingUpload.ResumeLayout();
                listBoxPendingUpload.EndUpdate();
            }
        }


        log.Info($"���� {UploadTrackerPaths.PendingPath} �ļ�");
        // д�� pending.json �ļ�,��ʧЧ�Ĳ��ᱻд��
        lock (_lockObj)
        {
            var state = new UploadState
            {
                AllFilesInFolder = currentFiles,
                FilesToUpload = pendingFiles?.ToList() ?? []
            };

            var pendingJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UploadTrackerPaths.PendingPath, pendingJson);
        }

        log.Info($"��ظ��£����ļ� {currentFiles.Count} �������ϴ� {pendingFiles?.Count} ��");
        await UploadPre();
    }
    #endregion

    #region �ϴ��߼�

    /// <summary>
    /// ��֤�ϴ�ǰ���Ⱦ�������
    /// </summary>
    /// <returns></returns>
    private Task<bool> ValidateUploadPrerequisites()
    {
        // �����ϴ��ļ��б��Ƿ�Ϊ��
        if (pendingFiles == null || pendingFiles.Count == 0) return Task.FromResult(false);

        // ��������
        if (!ValidateSettings())
        {
            log.Error("URL������Ч");
            LabelUploadHintPrint("���ô�����������");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// ��ȡ�ϴ��� HttpClient ʵ����
    /// </summary>
    /// <returns></returns>
    private void SetUploadClient()
    {
        // �趨 HttpClient ������ͷ
        HttpRetryHelper.ConfigureHttpClient(
            Properties.Settings.Default.XTenantId,
            Properties.Settings.Default.XTraceId,
            Properties.Settings.Default.XUserId,
            Properties.Settings.Default.XUserName
        );
    }

    /// <summary>
    /// ��ȡ���ͺ�
    /// </summary>
    /// <param name="oldPartsCode"></param>
    /// <returns></returns>
    private async Task<string?> QueryNewPartsCodeByOldPartsCode(string? oldPartsCode)
    {
        var httpClient = HttpRetryHelper.GetClient();

        log.Info($"���Ի�ȡ��Ӧ�����ͺ�...");
        log.Info($"���ʵ�ַ: {queryURL}?mbomCode={oldPartsCode}");

        var partsAttrValueQueryData = await HttpRetryHelper.RetryHttpRequestAsync<PartsAttrValuePostResponseModel>(
            async () => await httpClient.PostAsync(
                $"{queryURL}?mbomCode={oldPartsCode}",
                new StringContent(string.Empty)
            ),
            requestInfo: $"��ȡ���ͺ���Ϣ - mbomCode: {oldPartsCode}"
        );

        return partsAttrValueQueryData?.data?.First()?.partsCode ?? oldPartsCode;
    }

    /// <summary>
    /// ��ѯ�����б�
    /// </summary>
    /// <returns></returns>
    private async Task<MouldSizesCutterResponseModel?> QueryContainedMouldList(string? newPartsCode, string[]? fileNameParts)
    {
        string? specification = fileNameParts?[1].TrimEnd('F', 'Z');
        var httpClient = HttpRetryHelper.GetClient();

        log.Info($"��ѯ�ͺŹ���Ӧ��ID...");
        log.Info($"���ʵ�ַ: {writeURL}?partsCode={newPartsCode}&specification={specification}");

        // ���û�����ͺ�,��ʹ��ԭ�����ͺ�
        var queryData = await HttpRetryHelper.RetryHttpRequestAsync<MouldSizesCutterResponseModel>(
            async () => await httpClient.GetAsync(
                $"{writeURL}?partsCode={newPartsCode}&specification={specification}"
            ),
            requestInfo: $"��ѯ�ͺŹ�� - partsCode: {newPartsCode}, specification: {specification}"
        );

        if (queryData?.code != "00") throw new Exception($"��ѯ�ͺŹ��ʧ��: {queryData?.error} {queryData?.message}");

        // �������κ����ݵ�ʱ��˵���ϴ��Ŀ϶����µ��ͺŹ��,ֱ���ϴ�
        if (queryData?.data == null || queryData.data.Count == 0) return queryData;

        // �ļ����д��ڱ��,�����Ѵ���,�ǾͲ��ϴ���
        if (fileNameParts?.Length != 2 &&
            (queryData?.data?.Select(d => d?.fileName?.Split().Last()).Contains(fileNameParts?.Last()) ?? true))
            throw new Exception($"�ļ��Ѿ��������ͺŹ���У������ϴ�");

        return queryData;
    }

    /// <summary>
    /// ��ѯ��ģ�ߴ���,����д��ʱ��Ϊ����ʹ��
    /// </summary>
    /// <param name="newPartsCode"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task<MouldSizesListResponseModel?> QueryMouldSizeId(string? newPartsCode, string? specification)
    {
        var httpClient = HttpRetryHelper.GetClient();
        // ��ȡ��ģ���
        log.Info($"��ȡ��ģ���...");
        log.Info($"���ʵ�ַ: {listURL}?current=1&partsCode={newPartsCode}&size=15");
        var listData = await HttpRetryHelper.RetryHttpRequestAsync<MouldSizesListResponseModel>(
            async () => await httpClient.GetAsync(
                $"{listURL}?current=1&partsCode={newPartsCode}&size=15"
            ),
            requestInfo: $"��ȡ��ģ��� - partsCode: {newPartsCode}"
        );

        // �����ȡ��ģ���ʧ��,�������ϴ�
        if (listData?.code != "00") throw new Exception($"��ȡ��ģ���ʧ��: {listData?.errors} {listData?.message}");

        // ���û���ҵ���Ӧ�ĵ�ģ���,�������ϴ�
        if (listData?.data?.records?.All(record => double.Parse(record?.specification ?? "0") != double.Parse(specification ?? "0")) ?? true)
            throw new Exception($"û���ҵ���Ӧ�ĵ�ģ���,�����ͺź͹���Ƿ���¼��: {newPartsCode} {specification}");

        return listData;
    }

    /// <summary>
    /// �ϴ�֮ǰ��׼��
    /// </summary>
    /// <param name="responseInfos"></param>
    /// <returns></returns>
    private async Task PrepareToUpload(
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
        string watchPath = txtPath.Text;

        foreach (var file in pendingFiles ?? [])
        {
            try
            {
                // ����ļ��Ƿ����
                if (!File.Exists(Path.Combine(watchPath, file))) throw new Exception($"�ļ� {file} ������");

                // ��ȡ�ļ��������,�ļ����а����ͺź͹��
                string[]? fileNameParts = Path.GetFileNameWithoutExtension(file)?.Split();

                // Ϊ��ֹʹ��Эͬ�в����ڵľ��ͺ�,���ȳ��Ի�ȡ��Ӧ�����ͺ�
                var newPartsCode = await QueryNewPartsCodeByOldPartsCode(fileNameParts?[0]);

                // ��ѯ�����б�
                var queryData = await QueryContainedMouldList(newPartsCode, fileNameParts);

                //  ��ȡ��ģ���,����ʹ�������ͺ�ȥ��ѯ,���Կ��Դӽ����ȡ���ͺ�
                var listData = await QueryMouldSizeId(newPartsCode, fileNameParts?[1].TrimEnd('F', 'Z'));

                // ������µı�ż�����׼���ϴ�
                responseInfos?.Add((file, queryData, listData, null, null));
            }
            catch (Exception ex)
            {
                log.Error("�ϴ����̷�������", ex);
                LabelUploadHintPrint("�ϴ�ʧ�ܣ���鿴��־");
            }
        }
    }

    private MultipartFormDataContent? CreateUploadMultipartFormContent(
        string? file,
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)? responseInfo)
    {
        string watchPath = txtPath.Text;
        var form = new MultipartFormDataContent();
        // ��ȡ�ļ�����
        log.Info($"��ȡ�ļ�����...");
        if (file == null) throw new Exception("�ļ�·��Ϊ��");
        var fileContent = new ByteArrayContent(File.ReadAllBytes(Path.Combine(watchPath, file)));
        log.Info($"��ȡ�ļ�������ɣ��ļ���С: {fileContent.Headers.ContentLength} �ֽ�");

        // ��������ͷ
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");

        // �ļ������ļ����ݷֿ�,����Ǿ��ͺ����ϴ���ʱ���滻�ɵ�һ���ӿڲ���������ͺ�                
        form.Add(
            fileContent,
            "files",
            (responseInfo?.Item2?.code != "00") ?
            // ���û�в鵽���ݲ��ҿ����ϴ�,��ֱ��ʹ��ԭ�ļ���
            Path.GetFileName(file) :
            // ����鵽������,ֱ��ʹ�����ͺŵ��ļ���
            Path.GetFileName(file).Replace(
                Path.GetFileName(file).Split().First(),
                responseInfo?.Item3?.data?.records?[0].mouldName
            )
        );
        return form;
    }

    /// <summary>
    /// �ϴ��ļ�
    /// </summary>
    /// <param name="newPartsCode"></param>
    /// <param name="responseInfos"></param>
    /// <returns></returns>
    private async Task UploadFile(
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
        var httpClient = HttpRetryHelper.GetClient();
        // �ԺϷ��ļ������ϴ�,��tolist��һ����������,�������ϴ��������޸�ԭ�б����쳣
        foreach (var responseInfo in responseInfos?.ToList() ?? [])
        {
            var file = responseInfo?.Item1;
            try
            {
                var form = CreateUploadMultipartFormContent(file, responseInfo);

                // �����ϴ��ӿ�
                try
                {
                    var uploadResponse = await HttpRetryHelper.RetryHttpRequestAsync<FileStorageUploadParamResponseModel>(
                        async () => await httpClient.PostAsync(targetURL, form),
                        requestInfo: $"�ϴ��ļ� - {Path.GetFileName(file)}"
                    );

                    if (uploadResponse?.code == "00")
                    {
                        // ��Ӧ���������
#pragma warning disable CS8602 // �����ÿ��ܳ��ֿ����á�
                        responseInfos[
                            responseInfos.IndexOf(
                                responseInfos.First(response => response?.Item1 == file)
                            )
                        ] = (file, responseInfos.Last()?.Item2, responseInfos.Last()?.Item3, uploadResponse, null);
#pragma warning restore CS8602 // �����ÿ��ܳ��ֿ����á�
                    }
                    else
                    {
                        // ����ϴ�ʧ��,���responseInfos���Ƴ����ļ�
                        responseInfos?.Remove(responseInfos.First(response => response?.Item1 == file));
                    }
                }
                // ����ϴ�ʧ��,���responseInfos���Ƴ����ļ�
                catch (AggregateException)
                {
                    responseInfos?.Remove(responseInfos.First(response => response?.Item1 == file));
                }
            }
            catch (Exception ex)
            {
                LabelUploadHintPrint("�ϴ��쳣");
                log.Error("�ϴ��쳣", ex);
            }
        }
    }

    /// <summary>
    /// �ļ�д�������
    /// </summary>
    /// <param name="responseInfos"></param>
    /// <returns></returns>
    private async Task WriteFile(List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
        var httpClient = HttpRetryHelper.GetClient();
        // ���ϴ��ɹ����ļ�����д�����
        // �ϴ�ʧ�ܵĻᱻ�Ƴ�,д��ʱ�ܿ����Ķ����ϴ��ɹ���
        try
        {
            // �������Ķ����ϴ��ɹ����ļ�
            if (responseInfos?.Count == 0)
            {
                log.Info($"û���ϴ��ɹ����ļ�!����д��!");
                //MessageBox.Show("û���ϴ��ɹ����ļ�������д��\n������־����...");
                LabelUploadHintPrint("û���ϴ��ɹ����ļ�������д��\n������־����...");
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
                await File.WriteAllTextAsync(monthlyRecord, "[]");
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
            foreach (var responseInfo in responseInfos ?? [])
            {
                // ����д��ӿ���Ҫ������
                // ע������ӿڱ��밴���б�ȥ����
                List<MouldSizesCutterRequestModel> mouldSizesCutterRequestModelContentList = [];

                mouldSizesCutterRequestModelContentList.Add(new MouldSizesCutterRequestModel()
                {
                    // û�о���null
                    containerNum = responseInfo?.Item2?.data?.FirstOrDefault()?.containerNum,
                    // ������ù��,ȥ����β����ĸ,��ʾ����ֻ�����F��Z
                    cutterBlankSpec = responseInfo?.Item1?.Split()?[1].TrimEnd('F', 'Z'),
                    // ����β��F��Ϊ2,��Z���߲����Ķ���1
                    cutterType = (responseInfo?.Item1?.Split()?[1].ToArray().Last() == 'F') ? 2 : 1,
                    // ��Ҫʹ��Ĭ��ֵ,�п�ֱ���ÿ�
                    fileId = long.TryParse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId, out _) ? long.Parse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId) : null,
                    // ȡ����ʱ�򾡿����ÿ���Ľӿڵ���Ӧ,����м����޸����Ǵ���
                    fileName = responseInfo?.Item4?.data?.FirstOrDefault()?.fileName,
                    // �п�ֱ�����null
                    fileUrl = responseInfo?.Item4?.data?.FirstOrDefault()?.fileUrl,
                    // ��ģ���,���û�о�null
                    mouldSizeCutterId = long.TryParse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId, out _) ? long.Parse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId) : null,
                    // ��ģ��Ŵӵ�ģ����б��ѯ�ӿ��л�ȡ
                    mouldSizeId = long.Parse(responseInfo?.Item3?.data?.records?.First(record => record?.specification?.Split('.')?[0] == responseInfo?.Item1?.Split()?[1].TrimEnd('F', 'Z'))?.mouldSizeId ?? "0"),
                    // Ĭ��ʹ��1
                    seq = responseInfo?.Item2?.data?.FirstOrDefault()?.seq ?? 1
                });
                var jsonContent = JsonSerializer.Serialize(mouldSizesCutterRequestModelContentList, new JsonSerializerOptions { WriteIndented = true });
                log.Info($"{jsonContent}");

                // ��ʼд��
                HttpContent httpContent = new StringContent(jsonContent);
                httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                try
                {
                    var writeResponse = await HttpRetryHelper.RetryHttpRequestAsync<MouldSizesCutterPostResponseModel>(
                        async () => await httpClient.PostAsync(writeURL, httpContent),
                        requestInfo: $"д������ - {responseInfo?.Item1}"
                    );

                    if (writeResponse?.code != "00")
                    {
                        LabelUploadHintPrint($"д������ʧ��");
                        log.Warn($"д������ʧ��: {writeResponse?.error}");
                        // ʧ�ܺ�����һ��
                        continue;
                    }
                }
                catch (AggregateException)
                {
                    // ʧ�ܺ�����һ��
                    continue;
                }

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
                listBoxUploadComplete.Invoke(new Action(() =>
                {
                    listBoxUploadComplete.BeginUpdate();
                    listBoxUploadComplete.SuspendLayout();
                    listBoxUploadComplete.Items.Clear();
                    uploadedFiles?.ForEach(file => listBoxUploadComplete.Items.Add(Path.GetFileName(file.fileName)));
                    listBoxUploadComplete.ResumeLayout();
                    listBoxUploadComplete.EndUpdate();
                }));
            }
            else
            {
                listBoxUploadComplete.BeginUpdate();
                listBoxUploadComplete.SuspendLayout();
                listBoxUploadComplete.Items.Clear();
                uploadedFiles?.ForEach(file => listBoxUploadComplete.Items.Add(Path.GetFileName(file.fileName)));
                listBoxUploadComplete.ResumeLayout();
                listBoxUploadComplete.EndUpdate();
            }

            // ���б����ɾ�����ϴ��ļ��б�
            if (listBoxPendingUpload.InvokeRequired)
            {
                listBoxUploadComplete.Invoke(new Action(() =>
                {
                    listBoxPendingUpload.BeginUpdate();
                    listBoxPendingUpload.SuspendLayout();
                    responseInfos
                    ?.Where(file => file?.Item1 != null).ToList()
                    ?.ForEach(file => listBoxPendingUpload.Items.Remove(Path.GetFileName(file?.Item1) ?? ""));
                    listBoxUploadComplete.ResumeLayout();
                    listBoxUploadComplete.EndUpdate();
                }));
            }
            else
            {
                listBoxPendingUpload.BeginUpdate();
                listBoxPendingUpload.SuspendLayout();
                responseInfos
                ?.Where(file => file?.Item1 != null).ToList()
                ?.ForEach(file => listBoxPendingUpload.Items.Remove(Path.GetFileName(file?.Item1) ?? ""));
                listBoxUploadComplete.ResumeLayout();
                listBoxUploadComplete.EndUpdate();
            }

            // д�� pending.json
            await File.WriteAllTextAsync(UploadTrackerPaths.PendingPath, JsonSerializer.Serialize(pendingState, new JsonSerializerOptions { WriteIndented = true }));
            // д�����ϴ��ļ�
            await File.WriteAllTextAsync(monthlyRecord, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
            log.Info($"���� uploaded.json �ļ��ɹ�������� {uploadedFiles?.Count} ���ļ���");
            LabelUploadHintPrint($"�ϴ��ɹ�������� {uploadedFiles?.Count} ���ļ�,{listBoxPendingUpload.Items.Count}���ϴ�ʧ��");
            #endregion
        }
        catch (Exception ex)
        {
            log.Error("�����ϴ����ʱ��������", ex);
            //MessageBox.Show("�����ϴ����ʱ��������" + ex.Message);
            LabelUploadHintPrint("�����ϴ����ʱ��������");
            return;
        }
    }

    /// <summary>
    /// �ϴ������������ļ��ϴ��߼���
    /// </summary>
    /// <returns></returns>
    private async Task Upload()
    {
        // ���
        if (!await ValidateUploadPrerequisites()) return;

        // ��������
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos = new();
        string watchPath = txtPath.Text;

        // ����ͷ����
        SetUploadClient();

        try
        {
            // ��ȡ��Ҫ����
            await PrepareToUpload(responseInfos);

            // �ϴ��ļ�
            await UploadFile(responseInfos);

            // д�������
            await WriteFile(responseInfos);
        }
        catch (Exception ex)
        {
            log.Error("�ϴ����̷�������", ex);
            LabelUploadHintPrint("�ϴ�ʧ�ܣ���鿴��־");
        }
    }

    /// <summary>
    /// ���ϴ��������ʾ��ǩ����ʾ���������
    /// </summary>
    /// <param name="hint"></param>
    private void LabelUploadHintPrint(string hint)
    {
        if (labelUploadHint.InvokeRequired) labelUploadHint.Invoke(new Action(() => labelUploadHint.Text = hint));
        else labelUploadHint.Text = hint;
    }

    /// <summary>
    /// ȷ�����õ�URL������������Ч��
    /// </summary>
    /// <returns></returns>
    private bool ValidateSettings()
    {
        bool valid =
        !string.IsNullOrEmpty(targetURL) && Uri.TryCreate(targetURL, UriKind.Absolute, out _) &&
        !string.IsNullOrEmpty(writeURL) && Uri.TryCreate(writeURL, UriKind.Absolute, out _) &&
        !string.IsNullOrEmpty(queryURL) && Uri.TryCreate(queryURL, UriKind.Absolute, out _) &&
        !string.IsNullOrEmpty(listURL) && Uri.TryCreate(listURL, UriKind.Absolute, out _) &&
        !string.IsNullOrEmpty(Properties.Settings.Default.XTenantId) &&
        !string.IsNullOrEmpty(Properties.Settings.Default.XUserId) &&
        allowedExtensions != null && allowedExtensions.Length > 0 &&
        !string.IsNullOrEmpty(allowedFileNameRules);
        if (!valid)
        {
            log.Error("���ô���: Ŀ��URL��д��URL����ѯURL���б�URLδ����");
            LabelUploadHintPrint("���ô�����������");
        }
        else
        {
            log.Info("������֤ͨ��");
        }
        return valid;
    }
    #endregion
}
