using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public long? partsId { get; set; } = 0;
    public string? partsName { get; set; } = string.Empty;
    public long? productId { get; set; } = 0;
    public string? propCode { get; set; } = string.Empty;
    public long? propId { get; set; } = 0;
    public string? propName { get; set; } = string.Empty;
    public string? propValues { get; set; } = string.Empty;
}
