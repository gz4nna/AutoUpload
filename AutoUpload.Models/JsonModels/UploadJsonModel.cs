namespace AutoUpload.Models.JsonModels;

/// <summary>
/// 已上传文件的JSON模型
/// </summary>
public class UploadJsonModel
{
    public string fileName { get; set; } = string.Empty;
    public string uploadTime { get; set; } = string.Empty;
}
