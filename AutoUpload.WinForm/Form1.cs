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
        // 日志
        private static readonly ILog log = LogManager.GetLogger(typeof(Form1));

        // 从settings里面取用
        /// <summary>
        /// 上传文件的后缀名集合
        /// </summary>
        private string[]? allowedExtensions;
        /// <summary>
        /// 文件名合法规则
        /// </summary>
        private string allowedFileNameRules;

        // 私有成员变量
        /// <summary>
        /// 本地文件夹监视器
        /// </summary>
        private FileSystemWatcher? watcher;
        /// <summary>
        /// 上传的目标URL
        /// </summary>
        private string? targetURL;
        private string? writeURL;
        #endregion

        #region Init
        /// <summary>
        /// 初始化主要窗体的实例,并读取设置和绑定控件。
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            Init();
            InitWatcher(txtPath.Text);
        }

        /// <summary>
        /// 初始化主要窗体的实例,并读取设置和绑定控件。
        /// </summary>
        /// <returns></returns>
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
                }
                else
                {
                    MessageBox.Show($"{lastPath} 不存在, 请重新选择目录!","读取设置出错",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                    log.Warn($"{lastPath} 不存在, 请重新选择目录!");
                }

                log.Info($"读取配置文件: <TargetURL>...");
                targetURL = Properties.Settings.Default.TargetURL;
                log.Info($"读取配置文件: <TargetURL> {targetURL}");

                log.Info($"读取配置文件: <WriteURL>...");
                writeURL = Properties.Settings.Default.WriteURL;
                log.Info($"读取配置文件: <WriteURL> : {writeURL}");

                log.Info($"读取配置文件: <AllowedExtensions>...");
                allowedExtensions = Properties.Settings.Default.AllowedExtensions.Split("|");
                log.Info($"读取配置文件: <allowedExtensions> : {allowedExtensions}");

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

                this.notifyIcon = new NotifyIcon();
                this.contextMenuStrip = new ContextMenuStrip();
                this.menuShow = new ToolStripMenuItem("主界面");
                this.menuExit = new ToolStripMenuItem("退出");

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
                log.Info($"初始化控件完成!");
                #endregion
            }
            catch(Exception ex)
            {
                log.Error($"初始化主窗体失败: {ex.Message}");
            }
            log.Info($"初始化主窗体完成!");
            return true;
        }

        /// <summary>
        /// 初始化文件监视器，监控指定路径的文件变化事件。
        /// </summary>
        /// <param name="path">需要监视的目录</param>
        private void InitWatcher(string path)
        {
            // 清除之前的监视器
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            // 创建新的监视器实例
            watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                // 监视的内容包括文件名,最后写入时间和创建时间等
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

        #region UI事件
        /// <summary>
        /// 点击浏览按钮选择一个目录进行监控。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowsePath_Click(object sender, EventArgs e)
        {
            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = folderBrowserDialog.SelectedPath;
                // 保存路径到设置中
                Properties.Settings.Default.LastPath = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.Save();  
                log.Info($"选择路径: {folderBrowserDialog.SelectedPath}");
                InitWatcher(folderBrowserDialog.SelectedPath);                
            }
        }

        /// <summary>
        /// 上传按钮点击事件处理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnUpload_Click(object sender, EventArgs e)
        {
            #region 变量
            // 构造待上传文件列表
            string watchPath = txtPath.Text;
            List<string> pendingFiles = new();
            List<(string?, MouldSizesCutterResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?> 
                responseInfos = new();
            // 查询刀模ID和型号规格是否存在
            using var queryClient = new HttpClient();
            // 上传文件
            using var sendClient = new HttpClient();
            // 写入文件
            using var writeClient = new HttpClient();
            #endregion

            // 检查路径是否存在
            log.Info($"上传事件开始");
            log.Info($"检查路径是否存在: {watchPath}");
            if (File.Exists(UploadTrackerPaths.PendingPath))
            {
                try
                {
                    var pendingJson = await File.ReadAllTextAsync(UploadTrackerPaths.PendingPath);
                    var state = JsonSerializer.Deserialize<UploadState>(pendingJson);
                    if (state == null || state.FilesToUpload.Count == 0)
                    {
                        log.Warn("没有待上传的文件，跳过上传");
                        MessageBox.Show("没有待上传的文件");
                        return;
                    }
                    // 恢复成绝对路径
                    pendingFiles = state.FilesToUpload.Select(file => Path.Combine(watchPath, file)).ToList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("读取待上传文件列表失败：" + ex.Message);
                    log.Error("读取待上传文件列表失败", ex);
                    return;
                }
            }
            
            // 根据型号查询刀模ID添加头            
            queryClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
            queryClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
            queryClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
            queryClient.DefaultRequestHeaders.Add("X-Timestamp", DateTime.Now.ToString());

            // 检查本地的合法性以及服务器上的合法性
            foreach (var file in pendingFiles)
            {
                // 检查文件是否存在
                if (!File.Exists(file))
                {
                    log.Warn($"文件 {file} 不存在，跳过上传");
                    continue;
                }

                try
                {
                    // 获取文件名并拆分,文件名中包含型号和规格
                    string[]? fileNameParts = Path.GetFileNameWithoutExtension(file)?.Split();
                    log.Info($"访问地址: {writeURL}?partsCode={fileNameParts?[0]}&specification={fileNameParts?[1]}");

                    // 用get去查型号规格对应的ID
                    log.Info($"查询型号规格对应的ID...");
                    var queryResponse = await queryClient.GetAsync($"{writeURL}?partsCode={fileNameParts?[0]}&specification={fileNameParts?[1]}");
                    var queryResult = await queryResponse.Content.ReadAsStringAsync();
                    if (!queryResponse.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"查询失败：{queryResponse.StatusCode}\n{queryResult}");
                        log.Warn($"查询失败：{queryResult}");
                        continue;
                    }

                    // get结果
                    var queryData = JsonSerializer.Deserialize<MouldSizesCutterResponseModel>(queryResult);
                    log.Info($"查询结束,正在处理...");
                    
                    // 如果已有的型号规格中已经有了该编号文件,那就不上传了
                    if (queryData?.data.Count != 0 && (queryData?.data.Select(d => d.fileName.Split().Last()).Contains(fileNameParts?[2]) ?? true))
                    {
                        log.Info($"文件 {file} 已经存在于型号规格中，跳过上传");
                        continue;
                    }
                    // 如果是新的编号
                    responseInfos.Add((file, queryData, null, null));
                }
                catch (Exception)
                {
                    continue;
                }
            }

            // 对合法文件进行上传
            foreach(var file in responseInfos.Select(responses => responses?.Item1).ToList())
            {
                try
                {
                    // 读取文件内容
                    log.Info($"读取文件内容...");
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(file));
                    log.Info($"读取文件内容完成，文件大小: {fileContent.Headers.ContentLength} 字节");

                    // 设置请求头
                    fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
                    var form = new MultipartFormDataContent();
                    form.Add(fileContent, "file", Path.GetFileName(file));

                    // 调用上传接口                
                    sendClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
                    sendClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
                    sendClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
                    sendClient.DefaultRequestHeaders.Add("X-User-Name", Properties.Settings.Default.XUserName);

                    // 获取响应体内容
                    var response = await sendClient.PostAsync(targetURL, form);
                    var result = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        // 将result按照FileStorageUploadParamResponseModel读出来
                        var uploadResponse = JsonSerializer.Deserialize<FileStorageUploadParamResponseModel>(result);

                        if (uploadResponse?.code == "00")
                        {
                            log.Info($"上传文件成功:{result}");

                            // 响应结果存下来
                            responseInfos[responseInfos.IndexOf(responseInfos.First(response => response?.Item1 == file))] = (file, responseInfos.Last()?.Item2, uploadResponse, null);
                        }
                        else
                        {
                            // 如果上传失败,则从responseInfos中移除该文件
                            responseInfos.Remove(responseInfos.First(response => response?.Item1 == file));

                            MessageBox.Show($"上传文件失败:{response.StatusCode}\n{result}");
                            log.Warn($"上传文件失败:{result}");
                        }
                    }
                    else
                    {
                        // 如果上传失败,则从responseInfos中移除该文件
                        responseInfos.Remove(responseInfos.First(response => response?.Item1 == file));

                        MessageBox.Show($"上传文件失败:{response.StatusCode}\n{result}");
                        log.Warn($"上传文件失败:{result}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("上传出错：" + ex.Message);
                    log.Error("上传异常", ex);
                }
            }

            // 对上传成功的文件进行写入操作
            try
            {                
                if (responseInfos.Count == 0)
                {
                    log.Info($"没有上传成功的文件!跳过写入!");
                    MessageBox.Show("没有上传成功的文件，跳过写入\n请检查日志报错...");
                    return;
                }
                // 调用写入接口                
                writeClient.DefaultRequestHeaders.Add("X-Tenant-Id", Properties.Settings.Default.XTenantId);
                writeClient.DefaultRequestHeaders.Add("X-Trace-Id", Properties.Settings.Default.XTraceId);
                writeClient.DefaultRequestHeaders.Add("X-User-Id", Properties.Settings.Default.XUserId);
                
                // 构造写入接口需要的数据
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

                // 开始写入
                HttpContent httpContent = new StringContent(jsonContent);
                httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                var response = await writeClient.PostAsync(writeURL, httpContent);
                var result = await response.Content.ReadAsStringAsync();

                // 成功
                if (response.IsSuccessStatusCode)
                {
                    var writeResponse = JsonSerializer.Deserialize<MouldSizesCutterPostResponseModel>(result);
                    if (writeResponse?.code == "00") log.Info($"写入数据成功: {result}");
                    else
                    {
                        MessageBox.Show($"写入数据失败: {response.StatusCode}\n{result}");
                        log.Warn($"写入数据失败: {result}");
                        return;
                    }
                }
                else
                {
                    MessageBox.Show($"写入数据失败: {response.StatusCode}\n{result}");
                    log.Warn($"写入数据失败: {result}");
                    return;
                }

                // 这里只有成功会执行
                // 更新 uploaded.json 文件
                // 将 responseInfos 中的文件名添加到 uploaded.json 中
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
                        log.Warn("uploaded.json 读取失败，已重置为空");
                    }
                }

                // 文件名为空的情况不可能出现
                responseInfos.ForEach(response => uploadedFiles?.Add(Path.GetFileName(response?.Item1) ?? ""));

                File.WriteAllText(UploadTrackerPaths.UploadedPath, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info($"更新 uploaded.json 文件成功，已添加 {uploadedFiles?.Count} 个文件名");                
            }
            catch (Exception ex)
            {
                log.Error("处理上传结果时发生错误", ex);
                MessageBox.Show("处理上传结果时发生错误：" + ex.Message);
                return;
            }            
        }
        #endregion

        #region 窗体事件
        /// <summary>
        /// 关闭时最小化
        /// </summary>
        /// <param name="e"></param>
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
        /// 点击托盘图标时显示主窗口。
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
        /// 文件重命名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            log.Info($"[FileRenamed] {e.ChangeType}: {e.FullPath}");
            UpdateFileRecord();
        }

        /// <summary>
        /// 文件发生修改
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
        /// 更新待处理文件列表到 pending.json 文件中。
        /// </summary>
        private void UpdateFileRecord()
        {
            // 检查路径是否存在
            string watchPath = txtPath.Text;
            if (!Directory.Exists(watchPath)) return;

            // 满足后缀名规则和文件名规则
            var currentFiles = Directory
                .EnumerateFiles(watchPath)
                .Where(f => allowedExtensions?.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) ?? false)
                .Select(f => Path.GetFileName(f))
                .Where(name => Regex.Match(name, @allowedFileNameRules).Success)
                .ToList();

            // 加载已上传的文件列表
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
                    log.Warn("uploaded.json 读取失败，已重置为空");
                }
            }
            // 如果不存在 uploaded.json 文件，则创建一个空的
            else
            {
                uploadedFiles = new();
                File.WriteAllText(UploadTrackerPaths.UploadedPath, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
                log.Info("首次运行：已初始化 uploaded.json");
            }

            // 筛选出待上传的文件
            var pendingFiles = currentFiles
                .Where(name => !uploadedFiles.Contains(name))
                .ToList();

            // 写入 pending.json 文件
            var state = new UploadState
            {
                AllFilesInFolder = currentFiles,
                FilesToUpload = pendingFiles
            };
            var pendingJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UploadTrackerPaths.PendingPath, pendingJson);

            log.Info($"监控更新：总文件 {currentFiles.Count} 个，待上传 {pendingFiles.Count} 个");
        }
        #endregion        
    }
}
