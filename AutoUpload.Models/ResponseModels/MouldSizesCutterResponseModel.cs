using AutoUpload.Models.JsonModels.Helpers;
using System.Text.Json.Serialization;

namespace AutoUpload.Models.ResponseModels;

/// <summary>
/// 查询模具尺寸刀具响应模型
/// </summary>
public class MouldSizesCutterResponseModel
{
    public string? code { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleListOrStringConverter<MouldSizesCutterResponseModelData>))]
    public List<MouldSizesCutterResponseModelData>? data { get; set; } = default;
    public string? error { get; set; } = string.Empty;
    public string? message { get; set; } = string.Empty;
    public string? timestamp { get; set; } = string.Empty;
}

public class MouldSizesCutterResponseModelData
{
    public int? containerNum { get; set; } = 0;
    public string? createBy { get; set; } = string.Empty;
    public string? createByName { get; set; } = string.Empty;
    public DateTime? createTime { get; set; } = DateTime.MinValue;
    public int? cutterBlankSpec { get; set; } = 0;
    public int? cutterType { get; set; } = 0;
    public int? deleted { get; set; } = 0;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? fileId { get; set; } = string.Empty;
    public string? fileName { get; set; } = string.Empty;
    public string? fileUrl { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? mouldSizeCutterId { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? mouldSizeId { get; set; } = string.Empty;
    public int? seq { get; set; } = 0;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? tenantId { get; set; } = string.Empty;
    public string? updateBy { get; set; } = string.Empty;
    public string? updateByName { get; set; } = string.Empty;
    public DateTime? updateTime { get; set; } = DateTime.MinValue;
}

