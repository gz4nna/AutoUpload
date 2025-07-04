using AutoUpload.Models.JsonModels.Helpers;
using System.Text.Json.Serialization;

namespace AutoUpload.Models.ResponseModels;

public class FileStorageUploadParamResponseModel
{
    public string? code { get; set; } = string.Empty;
    public List<FileStorageUploadParamResponseModelData>? data { get; set; } = default;
    public string? error { get; set; } = string.Empty;
    public string? message { get; set; } = string.Empty;
    public string? timestamp { get; set; } = string.Empty;
}
public class FileStorageUploadParamResponseModelData
{
    public string? fileExtName { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? fileId { get; set; } = string.Empty;
    public string? fileName { get; set; } = string.Empty;
    public string? fileOriginName { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? fileSize { get; set; } = string.Empty;
    public string? fileUrl { get; set; } = string.Empty;
    public string? fileUrlAli { get; set; } = string.Empty;
    public int? height { get; set; } = 0;
    public int? width { get; set; } = 0;

}
