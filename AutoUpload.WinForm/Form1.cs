using AutoUpload.Models;
using AutoUpload.Models.Helpers;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;
using log4net;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    private readonly ConcurrentBag<string>? pendingFiles = [];
    private readonly object _lockObj = new();

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
            UploadHelper.Instance.TargetURL = Properties.Settings.Default.TargetURL;

            log.Info($"��ȡ�����ļ�: <WriteURL>...");
            UploadHelper.Instance.WriteURL = Properties.Settings.Default.WriteURL;

            log.Info($"��ȡ�����ļ�: <QueryURL>...");
            UploadHelper.Instance.QueryURL = Properties.Settings.Default.QueryURL;

            log.Info($"��ȡ�����ļ�: <ListURL>...");
            UploadHelper.Instance.ListURL = Properties.Settings.Default.ListURL;

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

            this.contextMenuStrip.Items.AddRange([
                this.menuShow,
                this.menuExit
            ]);

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
                        if (!fileName.Equals($"UploadRecords_{DateTime.Now:yyyyMM}.json")) continue;

                        var uploadedJson = File.ReadAllText(file);
                        var tmpfile = JsonSerializer.Deserialize<List<UploadJsonModel>>(uploadedJson);
                        // ֻҪ���ǿ�
                        if (tmpfile == null || tmpfile.Count == 0) continue;
                        // �����ϴ��ļ��б���
                        tmpfile.ForEach(fileName => uploadedFiles.Add(fileName));
                    }

                    if (uploadedFiles.Count == 0) throw new Exception("û���ҵ����ϴ����ļ���¼");

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

            // �ϴ��ļ������ඩ���¼�
            UploadHelper.Instance.LabelUploadHintPrint += LabelUploadHintPrint;
            UploadHelper.Instance.ListBoxUploaded += UpdateListBoxUploadComplete;
            UploadHelper.Instance.ListBoxPendingUpload += UpdateListBoxPendingUpload;
            #endregion
        }
        catch (Exception ex)
        {
            log.Error($"��ʼ��������ʧ��: {ex.Message}");
            LabelUploadHintPrint($"��ʼ��������ʧ�ܣ���鿴��־: {ex.Message}");
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
        labelPathHint.Text = ""; // �����ʾ�ı�

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
                    FilesToUpload = []
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
            .ToList() ?? [])
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
    /// Initiates the upload process for files, performing necessary validations and operations.
    /// </summary>
    /// <remarks>This method performs a series of steps to upload files, including validation of
    /// prerequisites, preparation of upload parameters, and the actual file upload. It logs any errors encountered
    /// during the process and provides feedback to the user.</remarks>
    /// <returns></returns>
    private async Task Upload()
    {
        // ����ϴ���ʾ�ı�
        labelPathHint.Text = "";

        // ���
        if (!ValidateUploadPrerequisites()) return;

        // Ԫ���б�����ϴ���Ϣ
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos = [];

        // ����ͷ����
        SetUploadClient();

        try
        {
            UploadHelper.Instance.watchPath = txtPath.Text;

            // ��ȡ��Ҫ����,�����׳��쳣,ֻ������־�м�¼
            responseInfos = UploadHelper.Instance.PrepareToUpload(
                pendingFiles,
                responseInfos
            );

            // �ϴ��ļ�
            responseInfos = await UploadHelper.Instance.UploadFile(
                responseInfos
            );

            // д�������
            await UploadHelper.Instance.WriteFile(
                responseInfos
            );
        }
        catch (Exception ex)
        {
            log.Error("�ϴ����̷�������", ex);
            LabelUploadHintPrint("�ϴ�ʧ�ܣ���鿴��־");
        }
    }

    #endregion

    #region ������������֤
    /// <summary>
    /// Validates the current configuration settings for URLs and other required parameters.
    /// </summary>
    /// <remarks>This method checks that all necessary URLs are non-empty and valid, and that other required
    /// settings such as tenant ID, user ID, allowed extensions, and file name rules are properly set. If any setting is
    /// invalid, an error is logged and a hint is displayed to the user.</remarks>
    /// <returns><see langword="true"/> if all required settings are valid and properly configured; otherwise, <see
    /// langword="false"/>.</returns>
    private bool ValidateSettings()
    {
        bool valid =
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

    /// <summary>
    /// Validates the prerequisites for uploading files.
    /// </summary>
    /// <remarks>This method checks whether there are files pending for upload and verifies the configuration
    /// settings.</remarks>
    /// <returns><see langword="true"/> if the prerequisites for uploading are met; otherwise, <see langword="false"/>.</returns>
    private bool ValidateUploadPrerequisites()
    {
        // �����ϴ��ļ��б��Ƿ�Ϊ��
        if (pendingFiles == null || pendingFiles.IsEmpty) return false;

        // ��������
        if (!ValidateSettings())
        {
            log.Error("URL������Ч");
            LabelUploadHintPrint("���ô�����������");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Configures the HTTP client with the necessary request headers for upload operations.
    /// </summary>
    /// <remarks>This method sets up the HTTP client by applying tenant, trace, user ID, and user name headers
    /// from the application settings. It is essential to call this method before performing any upload operations to
    /// ensure that the client is properly configured with the required headers.</remarks>
    private static void SetUploadClient()
    {
        // �趨 HttpClient ������ͷ
        HttpRetryHelper.ConfigureHttpClient(
            Properties.Settings.Default.XTenantId,
            Properties.Settings.Default.XTraceId,
            Properties.Settings.Default.XUserId,
            Properties.Settings.Default.XUserName
        );
    }

    #endregion

    /// <summary>
    /// ���ϴ��������ʾ��ǩ����ʾ���������
    /// </summary>
    /// <param name="hint"></param>
    private void LabelUploadHintPrint(string hint)
    {
        if (labelUploadHint.InvokeRequired) labelUploadHint.Invoke(new Action(() => labelUploadHint.Text += hint));
        else labelUploadHint.Text += hint;
    }

    /// <summary>
    /// Updates the list box to display the names of files that have been uploaded.
    /// </summary>
    /// <remarks>This method ensures that the list box is updated on the UI thread. It clears any existing
    /// items and adds the file names of the uploaded files.</remarks>
    /// <param name="uploadedFiles">A list of <see cref="UploadJsonModel"/> objects representing the uploaded files. Each file's name will be
    /// displayed in the list box.</param>
    private void UpdateListBoxUploadComplete(List<UploadJsonModel>? uploadedFiles)
    {
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
        LabelUploadHintPrint($"�ϴ��ɹ�������� {uploadedFiles?.Count} ���ļ�,{listBoxPendingUpload.Items.Count}���ϴ�ʧ��");
    }

    /// <summary>
    /// Updates the pending upload list box by removing items that have been processed.
    /// </summary>
    /// <remarks>This method checks if the update to the list box needs to be invoked on the UI thread. It
    /// removes items from the list box that have a non-null file path in the provided list.</remarks>
    /// <param name="responseInfos">A list of tuples containing information about files and their associated models. The first item in each tuple
    /// represents the file path, which is used to identify and remove the corresponding item from the list box.</param>
    private void UpdateListBoxPendingUpload(List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
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
    }

}
