using System.Globalization;
using System.Text.Json;

namespace AlbumRobot.Sync.Core;

/// <summary>
/// Normalizes the QCE JSON export message shape into the same local-only
/// RawQQMessage used by the Direct API provider.
/// </summary>
public static class QceJsonExportNormalizer
{
    public static IReadOnlyList<RawQQMessage> ReadMessages(JsonElement export, string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        if (export.ValueKind == JsonValueKind.Array)
        {
            return export.EnumerateArray()
                .Select((message, index) => NormalizeMessage(message, groupId, index + 1))
                .Where(message => message is not null)
                .Cast<RawQQMessage>()
                .ToArray();
        }

        if (TryGetProperty(export, "messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            return messages.EnumerateArray()
                .Select((message, index) => NormalizeMessage(message, groupId, index + 1))
                .Where(message => message is not null)
                .Cast<RawQQMessage>()
                .ToArray();
        }

        var single = NormalizeMessage(export, groupId, 1);
        return single is null ? Array.Empty<RawQQMessage>() : [single];
    }

    public static IReadOnlyList<RawQQMessage> ReadMessages(string json, string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return ReadMessages(document.RootElement, groupId);
    }

    private static RawQQMessage? NormalizeMessage(JsonElement message, string groupId, int sourceOrder)
    {
        if (message.ValueKind != JsonValueKind.Object) return null;

        var sender = TryGetProperty(message, "sender", out var senderValue) && senderValue.ValueKind == JsonValueKind.Object
            ? senderValue
            : default;
        var memberId = ReadString(sender, "uid");
        var nickname = FirstNonEmpty(
            ReadString(sender, "title"),
            ReadString(sender, "nickname"),
            ReadString(sender, "name"));

        return new RawQQMessage(
            groupId,
            ReadString(message, "id"),
            memberId,
            nickname,
            ReadTimestamp(message),
            BuildPayload(message))
        {
            Provider = "qce-json",
            MessageSequence = ReadString(message, "seq"),
            SourceOrder = sourceOrder,
            MessageKind = ReadString(message, "type") ?? "json",
        };
    }

    private static JsonElementLike BuildPayload(JsonElement message)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetProperty(message, "content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            return new JsonElementLike(values);
        }

        if (!TryGetProperty(content, "elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
        {
            return new JsonElementLike(values);
        }

        var normalizedElements = new List<object?>();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (!TryGetProperty(element, "data", out var data) || data.ValueKind != JsonValueKind.Object) continue;

            var normalizedData = ConvertJsonValue(data, omitRawContent: true);
            normalizedElements.Add(normalizedData);
            if (normalizedData is IReadOnlyDictionary<string, object?> dataFields)
            {
                foreach (var field in dataFields) values[field.Key] = field.Value;
            }

            var contentText = ReadString(data, "content");
            if (contentText is null) continue;
            try
            {
                using var nestedDocument = JsonDocument.Parse(contentText);
                values["content"] = ConvertJsonValue(nestedDocument.RootElement);
            }
            catch (JsonException)
            {
                // Non-JSON content is not a structured card and is ignored.
            }
        }

        values["elements"] = normalizedElements;
        return new JsonElementLike(values);
    }

    private static object? ConvertJsonValue(JsonElement value, bool omitRawContent = false)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var integer) ? integer : value.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            JsonValueKind.Array => value.EnumerateArray().Select(item => ConvertJsonValue(item, omitRawContent)).ToArray(),
            JsonValueKind.Object => ConvertObject(value, omitRawContent),
            _ => null,
        };
    }

    private static IReadOnlyDictionary<string, object?> ConvertObject(JsonElement value, bool omitRawContent)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (IsSensitiveField(property.Name)) continue;
            if (omitRawContent && property.Name.Equals("content", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                continue;
            }

            fields[property.Name] = ConvertJsonValue(property.Value, omitRawContent);
        }

        return fields;
    }

    private static bool IsSensitiveField(string name) =>
        name.Equals("token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("access_token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("cookie", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ReadTimestamp(JsonElement message)
    {
        var time = ReadString(message, "time");
        if (time is not null && DateTimeOffset.TryParse(
                time,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        var timestampText = ReadString(message, "timestamp");
        if (!long.TryParse(timestampText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp)) return null;
        try
        {
            return Math.Abs(timestamp) >= 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                : DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement value, string name, out JsonElement property)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out property)) return true;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in value.EnumerateObject())
            {
                if (candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static string? ReadString(JsonElement value, string name)
    {
        if (!TryGetProperty(value, name, out var property)) return null;
        return property.ValueKind switch
        {
            JsonValueKind.String => NormalizeString(property.GetString()),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    private static string? NormalizeString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
