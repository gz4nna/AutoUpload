using AutoUpload.Models.JsonModels.Helpers;
using System.Text.Json.Serialization;

namespace AutoUpload.Models.ResponseModels;

public class PartsAttrValuePostResponseModel
{
    public string? code { get; set; } = string.Empty;
    public List<PartsAttrValuePostResponseModelData>? data { get; set; } = default;
    public string? error { get; set; } = string.Empty;
    public string? message { get; set; } = string.Empty;
    public string? timestamp { get; set; } = string.Empty;
}

public class PartsAttrValuePostResponseModelData
{
    public string? partsCode { get; set; } = string.Empty;
    public string? partsId { get; set; } = string.Empty;
    public string? partsName { get; set; } = string.Empty;
    public string? productId { get; set; } = string.Empty;
    public string? propCode { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? propId { get; set; } = string.Empty;
    public string? propName { get; set; } = string.Empty;
    public string? propValues { get; set; } = string.Empty;
}


