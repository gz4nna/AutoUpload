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
            return new(); // 或 return new List<T>();
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

public class FlexibleDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return null;
            // 支持标准日期格式
            if (DateTime.TryParseExact(str, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(str, out dt))
                return dt;
            // 支持字符串形式的时间戳
            if (long.TryParse(str, out var ts))
            {
                // 判断时间戳长度，Java时间戳为毫秒，Unix时间戳为秒
                if (str.Length >= 13)
                    return DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
                else
                    return DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
            }
            throw new JsonException($"无法解析日期时间: {str}");
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            var ts = reader.GetInt64();
            // 判断时间戳长度，Java时间戳为毫秒，Unix时间戳为秒
            if (ts > 9999999999) // 毫秒级
                return DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
            else // 秒级
                return DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
        }
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        throw new JsonException("日期时间字段类型错误");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        else
            writer.WriteNullValue();
    }
}