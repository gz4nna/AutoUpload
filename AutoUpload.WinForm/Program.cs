using log4net;
using log4net.Config;

using System.IO;
using System.Reflection;

namespace AutoUpload.WinForm;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // load log4net config
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logPath))
        {
            Directory.CreateDirectory(logPath);
        }

        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

#if !DEBUG
        ILog log = LogManager.GetLogger(typeof(Program));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (sender, e) =>
        {
            log.Error("UI线程未处理异常", e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            log.Error("非UI线程未处理异常", ex);
        };
#endif
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}