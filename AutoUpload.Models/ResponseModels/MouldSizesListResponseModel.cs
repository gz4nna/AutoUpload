using AutoUpload.Models.JsonModels.Helpers;
using System.Text.Json.Serialization;

namespace AutoUpload.Models.ResponseModels;

/// <summary>
/// 查询模具尺寸列表响应模型
/// </summary>
public class MouldSizesListResponseModel
{
    public string? code { get; set; } = string.Empty;
    public MouldSizesListResponseDataModel? data { get; set; } = new MouldSizesListResponseDataModel();
    public string? errors { get; set; } = string.Empty;
    public string? message { get; set; } = string.Empty;
    public string? timestamp { get; set; } = string.Empty;
}

/// <summary>
/// 主数据模型
/// </summary>
public class MouldSizesListResponseDataModel
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? current { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? pages { get; set; } = string.Empty;
    public List<MouldSizesListResponseDataRecordsModel>? records { get; set; } = new();
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? size { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? total { get; set; } = string.Empty;
}

/// <summary>
/// 数据列表,包含多个规格
/// </summary>
public class MouldSizesListResponseDataRecordsModel
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? baseSpecification { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? categoryId { get; set; } = string.Empty;
    public string? categoryName { get; set; } = string.Empty;
    public string? copyPartsCode { get; set; } = string.Empty;
    public string? createBy { get; set; } = string.Empty;
    public string? createByName { get; set; } = string.Empty;
    public string? createTime { get; set; } = string.Empty;
    public int? eyelet { get; set; } = 0;
    public string? eyeletDesc { get; set; } = string.Empty;
    public string? factoryId { get; set; } = string.Empty;
    public string? factoryName { get; set; } = string.Empty;
    public string? fimageUrl { get; set; } = string.Empty;
    public string? groupCode { get; set; } = string.Empty;
    public string? groupName { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? mouldId { get; set; } = string.Empty;
    public string? mouldName { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? mouldSizeId { get; set; } = string.Empty;
    public List<MouldSizeImageVOModel>? mouldSizeImageVOList { get; set; } = new();
    public List<MouldSizeTermSVOModel>? mouldSizeTermSVOList { get; set; } = new();
    public string? orderCode { get; set; } = string.Empty;
    public string? orderNo { get; set; } = string.Empty;
    public string? partsCode { get; set; } = string.Empty;
    public string? partsCodeCreateTime { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? partsId { get; set; } = string.Empty;
    public string? partsName { get; set; } = string.Empty;
    public string? productline { get; set; } = string.Empty;
    public int? reCheckNum { get; set; } = 0;
    public int? reCheckStatus { get; set; } = 0;
    public string? reCheckStatusDesc { get; set; } = string.Empty;
    public int? rollFinish { get; set; } = 0;
    public int? sizeType { get; set; } = 0;
    public string? sizeTypeDesc { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? specification { get; set; } = string.Empty;
    public string? updateBy { get; set; } = string.Empty;
    public string? updateByName { get; set; } = string.Empty;
    public string? updateTime { get; set; } = string.Empty;
    public string? zimageUrl { get; set; } = string.Empty;
}

/// <summary>
/// 模具尺寸图片模型
/// </summary>
public class MouldSizeImageVOModel
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? imageFileId { get; set; } = string.Empty;
    public string? imageFileName { get; set; } = string.Empty;
    public string imageFileUrl { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? mouldSizeImageId { get; set; } = string.Empty;
    public int? seq { get; set; } = 0;
}

/// <summary>
/// 模具尺寸术语模型
/// </summary>
public class MouldSizeTermSVOModel
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? mouldSizeTermId { get; set; } = string.Empty;
    public int? seq { get; set; } = 0;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? sizeTermId { get; set; } = string.Empty;
    public string? sizeTermRemark { get; set; } = string.Empty;
    public string? termName { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? termValue { get; set; } = string.Empty;
}