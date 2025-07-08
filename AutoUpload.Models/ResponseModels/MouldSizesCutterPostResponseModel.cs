namespace AutoUpload.Models.ResponseModels;

/// <summary>
/// 写入模具尺寸刀具响应模型
/// </summary>
public class MouldSizesCutterPostResponseModel
{
    public string? code { get; set; } = string.Empty;
    public bool? data { get; set; } = default;
    public string? error { get; set; } = string.Empty;
    public string? message { get; set; } = string.Empty;
    public string? timestamp { get; set; } = string.Empty;
}
