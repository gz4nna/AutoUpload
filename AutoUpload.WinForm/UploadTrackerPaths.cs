using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoUpload.WinForm
{
    static class UploadTrackerPaths
    {
        public static string UploadedPath => Path.Combine(AppContext.BaseDirectory, "uploaded.json");
        public static string PendingPath => Path.Combine(AppContext.BaseDirectory, "pending.json");
    }

}
