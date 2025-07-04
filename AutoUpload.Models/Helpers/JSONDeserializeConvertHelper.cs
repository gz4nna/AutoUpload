using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoUpload.Models.JsonModels.Helpers;

/// <summary>
/// long类型转换为string类型的JsonConverter
/// </summary>
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

/// <summary>
/// string或List<T>类型转换的JsonConverter
/// </summary>
/// <typeparam name="T"></typeparam>
public class FlexibleListOrStringConverter<T> : JsonConverter<List<T>?>
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // 正常的数组
            return JsonSerializer.Deserialize<List<T>>(ref reader, options);
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            // 字符串，直接跳过
            reader.GetString(); // 读取但不使用
            return null; // 或 return new List<T>();
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        throw new JsonException("Unexpected token type for List<T> or string");
    }

    public override void Write(Utf8JsonWriter writer, List<T>? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}