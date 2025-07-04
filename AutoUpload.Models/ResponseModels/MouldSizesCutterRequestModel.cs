namespace AutoUpload.Models.ResponseModels;

/// <summary>
/// 写入模具尺寸刀具请求模型
/// 不需要针对long进行转换
/// </summary>
public class MouldSizesCutterRequestModel
{
    public int? containerNum { get; set; } = 0;
    public string? cutterBlankSpec { get; set; } = string.Empty;
    public int? cutterType { get; set; } = 0;
    public long fileId { get; set; } = 0;
    public string fileName { get; set; } = string.Empty;
    public string fileUrl { get; set; } = string.Empty;
    public long? mouldSizeCutterId { get; set; } = 0;
    public long? mouldSizeId { get; set; } = 0;
    public int? seq { get; set; } = 0;

}
