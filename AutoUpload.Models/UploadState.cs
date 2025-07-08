namespace AutoUpload.Models;

/// <summary>
/// 上传状态类
/// </summary>
public class UploadState
{
    public List<string> AllFilesInFolder { get; set; } = new();
    public List<string> FilesToUpload { get; set; } = new();
}
