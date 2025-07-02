using log4net;

namespace AutoUpload.Models.Helpers;
public class DelayFileSystemWatcherHelper : IDisposable
{
    private static readonly ILog log = LogManager.GetLogger(typeof(DelayFileSystemWatcherHelper));

    private readonly FileSystemWatcher watcher;
    private readonly System.Timers.Timer delayTimer;
    private readonly object lockObj = new();
    private bool eventPending = false;
    private string? lastChangedFile = null;

    private HashSet<string> currentCatchFiles = new();

    /// <summary>
    /// 事件：延时后触发
    /// </summary>
    public event Action<string?>? DelayChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="path">监控目录</param>
    /// <param name="filter">文件过滤，如"*"或"*.dxf"</param>
    /// <param name="delayMs">延时毫秒数，默认500</param>
    public DelayFileSystemWatcherHelper(string path, string filter = "*.dxf", double delayMs = 500)
    {
        watcher = new FileSystemWatcher(path, filter)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;

        delayTimer = new System.Timers.Timer(delayMs);
        delayTimer.AutoReset = false;
        delayTimer.Elapsed += (s, e) =>
        {
            string? file;
            lock (lockObj)
            {
                file = string.Join("|", currentCatchFiles);
                currentCatchFiles.Clear();
                eventPending = false;
            }
            DelayChanged?.Invoke(file);
        };
    }

    public void Start() => watcher.EnableRaisingEvents = true;
    public void Stop() => watcher.EnableRaisingEvents = false;

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (lockObj)
        {
            log.Info($"[FileChanged] {e.ChangeType}: {e.FullPath}");
            lastChangedFile = e.FullPath;
            if (!currentCatchFiles.Contains(lastChangedFile))
            {
                currentCatchFiles.Add(lastChangedFile);
            }
            eventPending = true;
            delayTimer.Stop();
            delayTimer.Start();
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        lock (lockObj)
        {
            log.Info($"[FileRenamed] {e.ChangeType}: {e.FullPath}");
            lastChangedFile = e.FullPath;
            if (!currentCatchFiles.Contains(lastChangedFile))
            {
                currentCatchFiles.Add(lastChangedFile);
            }
            eventPending = true;
            delayTimer.Stop();
            delayTimer.Start();
        }
    }

    public void Dispose()
    {
        watcher.Dispose();
        delayTimer.Dispose();
    }
}
