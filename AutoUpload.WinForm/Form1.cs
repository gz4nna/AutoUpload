using AutoUpload.Models;
using AutoUpload.Models.Helpers;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;
using log4net;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoUpload.WinForm;

/// <summary>
/// 主窗体类，负责界面初始化、控件事件处理、文件监控、上传流程控制等。
/// 该类集成了文件夹监控、上传队列管理、上传进度反馈、日志输出等核心功能。
/// </summary>
public partial class Form1 : Form, IDisposable
{
    #region 成员变量
    /// <summary>
    /// 日志记录器。
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(Form1));
    /// <summary>
    /// 每小时执行一次的 System.Threading.Timer 定时器。
    /// </summary>
    private System.Threading.Timer? timer;

    /// <summary>
    /// 资源释放标记。
    /// </summary>
    private bool disposed = false;

    /// <summary>
    /// 上传文件的后缀名集合。
    /// </summary>
    private string[]? allowedExtensions;
    /// <summary>
    /// 文件名合法规则（正则表达式）。
    /// </summary>
    private string? allowedFileNameRules;

    /// <summary>
    /// 本地文件夹监视器，负责监控指定目录下的文件变化。
    /// </summary>
    private DelayFileSystemWatcherHelper? watcher;
    /// <summary>
    /// 待上传的文件列表，线程安全集合。
    /// </summary>
    private readonly ConcurrentBag<string>? pendingFiles = [];
    /// <summary>
    /// 线程锁对象，用于多线程安全操作待上传文件集合。
    /// </summary>
    private readonly object _lockObj = new();
    #endregion

    #region 初始化
    /// <summary>
    /// 构造函数，初始化主窗体实例，加载配置并绑定控件。
    /// </summary>
    public Form1()
    {
        InitializeComponent();
        Init();
        InitWatcher(txtPath.Text);
    }

    /// <summary>
    /// 初始化主窗体，读取配置、初始化控件、加载上传记录、注册事件。
    /// </summary>
    /// <returns>初始化成功返回true，否则返回false。</returns>
    public bool Init()
    {
        log.Info($"初始化主窗口...");

        try
        {
            #region 读取设置
            log.Info($"读取配置文件: <LastPath>...");
            var lastPath = Properties.Settings.Default.LastPath;
            if (Directory.Exists(lastPath))
            {
                txtPath.Text = lastPath;
                log.Info($"读取配置文件: <LastPath> : {lastPath}");
                labelPathHint.Text = $"当前监控目录: {lastPath}";
            }
            else
            {
                MessageBox.Show($"{lastPath} 不存在, 请重新选择目录!", "读取设置出错", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                log.Warn($"{lastPath} 不存在, 请重新选择目录!");
            }

            log.Info($"读取配置文件: <TargetURL>...");
            UploadHelper.Instance.TargetURL = Properties.Settings.Default.TargetURL;

            log.Info($"读取配置文件: <WriteURL>...");
            UploadHelper.Instance.WriteURL = Properties.Settings.Default.WriteURL;

            log.Info($"读取配置文件: <QueryURL>...");
            UploadHelper.Instance.QueryURL = Properties.Settings.Default.QueryURL;

            log.Info($"读取配置文件: <ListURL>...");
            UploadHelper.Instance.ListURL = Properties.Settings.Default.ListURL;

            log.Info($"读取配置文件: <AllowedExtensions>...");
            allowedExtensions = Properties.Settings.Default.AllowedExtensions.Split("|");
            log.Info($"读取配置文件: <allowedExtensions> : {Properties.Settings.Default.AllowedExtensions}");

            log.Info($"读取配置文件: <AllowedFileNameRules>...");
            allowedFileNameRules = Properties.Settings.Default.AllowedFileNameRules;
            log.Info($"读取配置文件: <AllowedFileNameRules> : {allowedFileNameRules}");
            #endregion

            #region 控件初始化
            log.Info($"初始化控件...");
#if DEBUG
            this.txtPath.ReadOnly = false;
#else
            this.txtPath.ReadOnly = true; // 发布版本中禁止直接在文本框中修改路径,可以通过UI设置来修改
#endif
            // 最小化到托盘
            this.notifyIcon = new NotifyIcon();
            this.contextMenuStrip = new ContextMenuStrip();
            this.menuShow = new ToolStripMenuItem("主界面");
            this.menuExit = new ToolStripMenuItem("退出");

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

            // 定时上传
            timer = new System.Threading.Timer(
                async _ => await UploadPre(),
                null,
                TimeSpan.Zero, // 立即执行
                TimeSpan.FromHours(1) // 每小时执行一次
            );
            log.Info($"初始化控件完成!");

            // 在当前目录下创建一个upload文件夹
            if (!Directory.Exists(UploadTrackerPaths.UploadFolder))
            {
                Directory.CreateDirectory(UploadTrackerPaths.UploadFolder);
                log.Info($"创建上传文件夹: {UploadTrackerPaths.UploadFolder}");
            }
            // 加载已上传的文件列表
            var uploadedFiles = new HashSet<UploadJsonModel>();

            // 在文件夹中读取全部上传文件
            if (Directory.Exists(UploadTrackerPaths.UploadFolder))
            {
                try
                {
                    // 每个文件
                    foreach (var file in Directory.EnumerateFiles(UploadTrackerPaths.UploadFolder))
                    {
                        var fileName = Path.GetFileName(file);
                        // 如果是这个月的
                        // UploadRecords_202506.json
                        if (!fileName.Equals($"UploadRecords_{DateTime.Now:yyyyMM}.json")) continue;

                        var uploadedJson = File.ReadAllText(file);
                        var tmpfile = JsonSerializer.Deserialize<List<UploadJsonModel>>(uploadedJson);
                        // 只要不是空
                        if (tmpfile == null || tmpfile.Count == 0) continue;
                        // 塞到上传文件列表中
                        tmpfile.ForEach(fileName => uploadedFiles.Add(fileName));
                    }

                    if (uploadedFiles.Count == 0) throw new Exception("没有找到已上传的文件记录");

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
                    log.Warn("uploaded.json 读取失败，已重置为空");
                    LabelUploadHintPrint("uploaded.json 读取失败，已重置为空");
                }
            }

            // 上传文件帮助类订阅事件
            UploadHelper.Instance.LabelUploadHintPrint += LabelUploadHintPrint;
            UploadHelper.Instance.ListBoxUploaded += UpdateListBoxUploadComplete;
            UploadHelper.Instance.ListBoxPendingUpload += UpdateListBoxPendingUpload;
            #endregion
        }
        catch (Exception ex)
        {
            log.Error($"初始化主窗体失败: {ex.Message}");
            LabelUploadHintPrint($"初始化主窗体失败，请查看日志: {ex.Message}");
        }
        log.Info($"初始化主窗体完成!");
        return true;
    }

    /// <summary>
    /// 初始化文件监视器，监控指定路径的文件变化事件。
    /// </summary>
    /// <param name="path">需要监视的目录路径。</param>
    private void InitWatcher(string path)
    {
        // 清除之前的监视器
        watcher?.Dispose();

        // 创建新的监视器实例
        watcher = new DelayFileSystemWatcherHelper(path, "*.*", 800);

        watcher.DelayChanged += (changedFile) =>
        {
            // 这里是延时合并后的回调
            this.Invoke(new Action(() => UpdateFileRecord(changedFile ?? "")));
        };
        watcher.Start();
    }
    #endregion

    #region 控件事件
    /// <summary>
    /// 设置按钮点击事件处理，切换到设置页面。
    /// </summary>
    /// <param name="sender">事件源对象。</param>
    /// <param name="e">事件参数。</param>
    private void btnSetting_Click(object sender, EventArgs e)
    {
        log.Info($"切换到设置页面");
        this.tabControl.SelectedTab = this.tabPageSetting;
    }

    /// <summary>
    /// 浏览按钮点击事件，弹出文件夹选择对话框，选择监控目录。
    /// </summary>
    /// <param name="sender">事件源对象。</param>
    /// <param name="e">事件参数。</param>
    private void btnBrowsePath_Click(object sender, EventArgs e)
    {
        log.Info($"浏览按钮被点击，选择目录...");
        labelPathHint.Text = ""; // 清空提示文本

        using var folderBrowserDialog = new FolderBrowserDialog();
        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            txtPath.Text = folderBrowserDialog.SelectedPath;
            // 保存路径到设置中
            Properties.Settings.Default.LastPath = folderBrowserDialog.SelectedPath;
            Properties.Settings.Default.Save();
            log.Info($"选择路径: {folderBrowserDialog.SelectedPath}");
            labelPathHint.Text = $"当前监控目录: {folderBrowserDialog.SelectedPath}";
            InitWatcher(folderBrowserDialog.SelectedPath);
        }
    }

    /// <summary>
    /// 日志按钮点击事件，切换到日志页面并打开日志目录。
    /// </summary>
    /// <param name="sender">事件源对象。</param>
    /// <param name="e">事件参数。</param>
    private void btnLog_Click(object sender, EventArgs e)
    {
        log.Info($"切换到日志页面");
        this.tabControl.SelectedTab = this.tabPageLog;
        // 打开指定目录
        if (Directory.Exists(UploadTrackerPaths.LogFolder))
        {
            System.Diagnostics.Process.Start("explorer.exe", UploadTrackerPaths.LogFolder);
            log.Info($"打开日志目录: {UploadTrackerPaths.LogFolder}");
        }
        else
        {
            log.Error($"日志目录不存在: {UploadTrackerPaths.LogFolder}");
        }
    }

    /// <summary>
    /// 上传按钮点击事件，触发上传流程。
    /// </summary>
    /// <param name="sender">事件源对象。</param>
    /// <param name="e">事件参数。</param>
    private async void btnUpload_Click(object sender, EventArgs e)
    {
        string watchPath = txtPath.Text;

        // 检查路径是否存在
        log.Info($"上传事件开始");
        log.Info($"检查路径是否存在: {watchPath}");
        if (File.Exists(UploadTrackerPaths.PendingPath))
        {
            try
            {
                UploadState? state;
                lock (_lockObj)
                {
                    var pendingJson = File.ReadAllText(UploadTrackerPaths.PendingPath);
                    state = JsonSerializer.Deserialize<UploadState>(pendingJson);
                }

                if (state == null || state.FilesToUpload.Count == 0)
                {
                    log.Warn("没有待上传的文件，跳过上传");
                    LabelUploadHintPrint("没有待上传的文件");
                    return;
                }

                // 恢复成绝对路径
                lock (_lockObj)
                {
                    // 清空当前集合
                    pendingFiles?.Clear();
                    state.FilesToUpload.ForEach(file => pendingFiles?.Add(Path.Combine(watchPath, file)));
                }

                // 换页
                this.tabControl.SelectedTab = this.tabPageUpload;
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show("读取待上传文件列表失败：" + ex.Message);
#elif RELEASE
                LabelUploadHintPrint("读取待上传文件列表失败");
#endif
                log.Error("读取待上传文件列表失败", ex);
                // 换页
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
    /// 上传前的预处理方法，调用 Upload 方法进行文件上传操作。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task UploadPre() => await Upload();

    /// <summary>
    /// 退出按钮点击事件，释放资源并关闭应用程序。
    /// </summary>
    /// <param name="sender">事件源对象。</param>
    /// <param name="e">事件参数。</param>
    private void btnExit_Click(object sender, EventArgs e)
    {
        try
        {
            Dispose();
            Application.Exit();
        }
        catch (Exception ex)
        {
            log.Error($"退出程序时发生错误!", ex);
        }
    }
    #endregion

    #region 窗体事件
    /// <summary>
    /// 窗体关闭事件，用户关闭时最小化到托盘。
    /// </summary>
    /// <param name="e">窗体关闭事件参数。</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 如果是用户关闭窗口，则最小化到托盘
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
            notifyIcon.ShowBalloonTip(1000, "后台运行中", "程序已最小化至托盘", ToolTipIcon.Info);
        }
        base.OnFormClosing(e);
    }

    /// <summary>
    /// 显示主窗口（从托盘恢复）。
    /// </summary>
    private void ShowMainWindow()
    {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.BringToFront();
    }
    #endregion

    #region 监控事件
    /// <summary>
    /// 监控文件夹变化，更新待上传文件列表并同步到界面。
    /// </summary>
    /// <param name="changedFilePath">发生变化的文件路径，多个路径用|分隔。</param>
    private async void UpdateFileRecord(string changedFilePath = "")
    {
        log.Info($"UpdateFileRecord: {changedFilePath}");
        // 检查路径是否存在
        string watchPath = txtPath.Text;
        var currentFiles = Directory.EnumerateFiles(watchPath)
            .Select(file => Path.GetFileName(file))
            .Where(file => allowedExtensions?
                .Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ?? false &&
                Regex.Match(Path.GetFileName(file), @allowedFileNameRules).Success)
            .ToList();
        log.Info($"已加载 {watchPath} 下所有文件,共 {currentFiles.Count} 个符合规则的文件");

        // 先放入已有待上传
        log.Info($"读取待上传文件列表: {UploadTrackerPaths.PendingPath}");
        // 不存在时创建一个新的
        if (!File.Exists(UploadTrackerPaths.PendingPath))
        {
            log.Warn($"没有找到待上传文件列表: {UploadTrackerPaths.PendingPath}");
            // 如果不存在就创建一个新的
            await File.WriteAllTextAsync(
                UploadTrackerPaths.PendingPath,
                JsonSerializer.Serialize(new UploadState
                {
                    AllFilesInFolder = currentFiles,
                    FilesToUpload = []
                }, new JsonSerializerOptions { WriteIndented = true }
            ));
            log.Info($"创建新的待上传文件列表: {UploadTrackerPaths.PendingPath}");
        }
        log.Info($"去掉当前目录下已不存在的文件");

        // 存在待上传并且在当前目录下能找到这些
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
            log.Info($"处理文件变化: {filePath}");
            // 排除掉被触发时为删除文件的情况
            if (!File.Exists(filePath))
            {
                // 从待上传列表中移除
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

            // 添加文件之前判断是否符合后缀规则和命名规则
            if (allowedExtensions?
                .Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase) ?? false &&
                // 符合名称规则
                Regex.Match(Path.GetFileName(filePath), @allowedFileNameRules).Success)
            {
                lock (_lockObj)
                {
                    // 添加文件之前先去重
                    if (!pendingFiles?.Contains(Path.GetFileName(filePath)) ?? true)
                    {
                        pendingFiles?.Add(Path.GetFileName(filePath));
                    }
                }
            }

            log.Info($"添加已修改文件: {Path.GetFileName(filePath)}");
        }

        log.Info("在列表框中显示待上传文件列表");
        // 使用临时列表避免长时间占用锁
        List<string> filesToProcess;
        lock (_lockObj)
        {
            filesToProcess = pendingFiles?.ToList() ?? [];
        }
        if (filesToProcess?.Count != 0)
        {
            // 在列表框中显示待上传文件列表
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


        log.Info($"更新 {UploadTrackerPaths.PendingPath} 文件");
        // 写入 pending.json 文件,已失效的不会被写入
        lock (_lockObj)
        {
            var state = new UploadState
            {
                AllFilesInFolder = currentFiles,
                FilesToUpload = pendingFiles?.ToList() ?? []
            };

            var pendingJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            lock (_lockObj)
            {
                File.WriteAllText(UploadTrackerPaths.PendingPath, pendingJson);
            }
        }

        log.Info($"监控更新：总文件 {currentFiles.Count} 个，待上传 {pendingFiles?.Count} 个");
        await UploadPre();
    }
    #endregion

    #region 上传逻辑
    /// <summary>
    /// 执行上传流程，包括校验、参数准备、文件上传和结果写入。
    /// </summary>
    /// <remarks>
    /// 步骤包括：
    /// 1. 校验上传前置条件（如配置、待上传文件等）。
    /// 2. 调用帮助类准备上传参数。
    /// 3. 上传文件并处理返回结果。
    /// 4. 写入上传结果并更新界面。
    /// 5. 发生异常时记录日志并提示用户。
    /// </remarks>
    /// <returns>异步任务。</returns>
    private async Task Upload()
    {
        // 清空上传提示文本
        labelPathHint.Text = "";

        // 检查
        if (!ValidateUploadPrerequisites()) return;

        // 元组列表放置上传信息
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos = [];

        // 请求头设置
        SetUploadClient();

        try
        {
            UploadHelper.Instance.WatchPath = txtPath.Text;

            // 获取必要参数,不会抛出异常,只会在日志中记录
            responseInfos = await UploadHelper.Instance.PrepareToUpload(
                pendingFiles,
                responseInfos
            );

            // 上传文件
            responseInfos = await UploadHelper.Instance.UploadFile(
                responseInfos
            );

            // 写入服务器
            await UploadHelper.Instance.WriteFile(
                responseInfos
            );
        }
        catch (Exception ex)
        {
            log.Error("上传过程发生错误", ex);
            LabelUploadHintPrint("上传失败，请查看日志");
        }
    }

    #endregion

    #region 参数设置与验证
    /// <summary>
    /// 校验当前配置项（如URL、后缀名、文件名规则等）是否合法。
    /// </summary>
    /// <returns>配置合法返回true，否则返回false。</returns>
    private bool ValidateSettings()
    {
        bool valid =
        !string.IsNullOrEmpty(Properties.Settings.Default.XTenantId) &&
        !string.IsNullOrEmpty(Properties.Settings.Default.XUserId) &&
        allowedExtensions != null && allowedExtensions.Length > 0 &&
        !string.IsNullOrEmpty(allowedFileNameRules);
        if (!valid)
        {
            log.Error("配置错误: 目标URL、写入URL、查询URL或列表URL未设置");
            LabelUploadHintPrint("配置错误，请检查设置");
        }
        else
        {
            log.Info("配置验证通过");
        }
        return valid;
    }

    /// <summary>
    /// 校验上传前置条件（如待上传文件、配置等）。
    /// </summary>
    /// <returns>前置条件满足返回true，否则返回false。</returns>
    private bool ValidateUploadPrerequisites()
    {
        // 检查待上传文件列表是否为空
        if (pendingFiles == null || pendingFiles.IsEmpty) return false;

        // 配置项检查
        if (!ValidateSettings())
        {
            log.Error("URL配置无效");
            LabelUploadHintPrint("配置错误，请检查设置");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 配置HttpClient请求头，供上传操作使用。
    /// </summary>
    private static void SetUploadClient()
    {
        // 设定 HttpClient 的请求头
        HttpRetryHelper.ConfigureHttpClient(
            Properties.Settings.Default.XTenantId,
            Properties.Settings.Default.XTraceId,
            Properties.Settings.Default.XUserId,
            Properties.Settings.Default.XUserName
        );
    }

    #endregion

    /// <summary>
    /// 在上传界面的提示标签中显示输入的内容。
    /// </summary>
    /// <param name="hint">要显示的提示文本。</param>
    private void LabelUploadHintPrint(string hint)
    {
        if (labelUploadHint.InvokeRequired) labelUploadHint.Invoke(new Action(() => labelUploadHint.Text += "\n" + hint));
        else labelUploadHint.Text += hint;
    }

    /// <summary>
    /// 更新已上传文件列表框，显示所有已上传文件。
    /// </summary>
    /// <param name="uploadedFiles">已上传文件的模型列表。</param>
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
        LabelUploadHintPrint($"上传成功，已添加 {uploadedFiles?.Count} 个文件,{listBoxPendingUpload.Items.Count}个上传失败");
    }

    /// <summary>
    /// 更新待上传文件列表框，移除已处理的文件。
    /// </summary>
    /// <param name="responseInfos">
    /// 包含待处理文件及其相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件路径。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    private void UpdateListBoxPendingUpload(List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos)
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
                listBoxPendingUpload.ResumeLayout();
                listBoxPendingUpload.EndUpdate();
            }));
        }
        else
        {
            listBoxPendingUpload.BeginUpdate();
            listBoxPendingUpload.SuspendLayout();
            responseInfos
            ?.Where(file => file?.Item1 != null).ToList()
            ?.ForEach(file => listBoxPendingUpload.Items.Remove(Path.GetFileName(file?.Item1) ?? ""));
            listBoxPendingUpload.ResumeLayout();
            listBoxPendingUpload.EndUpdate();
        }
    }

}
