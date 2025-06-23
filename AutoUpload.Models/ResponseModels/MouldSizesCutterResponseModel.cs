using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoUpload.Models.ResponseModels;

public class MouldSizesCutterResponseModel
{
    public string code { get; set; } = string.Empty;
    public List<MouldSizesCutterResponseModelData> data { get; set; } = default;
    public string error { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
}

public class MouldSizesCutterResponseModelData
{
    public int containerNum { get; set; } = 0;
    public string createBy { get; set; } = string.Empty;
    public string createByName { get; set; } = string.Empty;
    public DateTime createTime { get; set; } = DateTime.MinValue;
    public int cutterBlankSpec { get; set; } = 0;
    public int cutterType { get; set; } = 0;
    public int deleted { get; set; } = 0;
    public long fileId { get; set; } = 0;
    public string fileName { get; set; } = string.Empty;
    public string fileUrl { get; set; } = string.Empty;
    public long mouldSizeCutterId { get; set; } = 0;
    public long mouldSizeId { get; set; } = 0;
    public int seq { get; set; } = 0;
    public long tenantId { get; set; } = 0;
    public string updateBy { get; set; } = string.Empty;
    public string updateByName { get; set; } = string.Empty;
    public DateTime updateTime { get; set; } = DateTime.MinValue;
}
