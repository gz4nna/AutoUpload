using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoUpload.Models.ResponseModels;

public class MouldSizesCutterPostResponseModel
{
    public string code { get; set; } = string.Empty;
    public bool data { get; set; } = default;
    public string error { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
}
