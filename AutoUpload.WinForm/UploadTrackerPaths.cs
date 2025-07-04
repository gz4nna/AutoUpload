namespace AutoUpload.WinForm
{
    static class UploadTrackerPaths
    {
        public static string UploadFolder => Path.Combine(AppContext.BaseDirectory, "Uploads");
        public static string PendingPath => Path.Combine(AppContext.BaseDirectory, "pending.json");
        public static string LogFolder => Path.Combine(AppContext.BaseDirectory, "logs");
    }

}
