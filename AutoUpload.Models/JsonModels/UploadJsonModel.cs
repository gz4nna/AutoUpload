using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoUpload.Models.JsonModels;

public class UploadJsonModel
{
    public string fileName { get; set; } = string.Empty;
    public string uploadTime { get; set; } = string.Empty;
}
