using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public string? partsId { get; set; } = string.Empty;
    public string? partsName { get; set; } = string.Empty;
    public string? productId { get; set; } = string.Empty;
    public string? propCode { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? propId { get; set; } = string.Empty;
    public string? propName { get; set; } = string.Empty;
    public string? propValues { get; set; } = string.Empty;
}

public class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt64().ToString();
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
