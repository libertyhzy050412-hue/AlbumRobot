using System.Globalization;
using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed record QceMemberSnapshot(
    string MemberId,
    string? Uin,
    string? Nickname,
    string? CardName,
    int? Role)
{
    public string? DisplayName => FirstNonEmpty(CardName, Nickname);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

/// <summary>
/// Converts the verified QCE message envelope into local-only domain messages.
/// It deliberately does not create fallback IDs when QCE omits one.
/// </summary>
public static class QceMessageNormalizer
{
    public static IReadOnlyDictionary<string, QceMemberSnapshot> ReadMembers(JsonElement response)
    {
        var result = new Dictionary<string, QceMemberSnapshot>(StringComparer.Ordinal);
        if (!TryGetDataArray(response, "members", out var members)) return result;

        foreach (var member in members.EnumerateArray())
        {
            if (member.ValueKind != JsonValueKind.Object) continue;

            var memberId = ReadString(member, "uid");
            if (string.IsNullOrWhiteSpace(memberId)) continue;

            var snapshot = new QceMemberSnapshot(
                memberId,
                ReadString(member, "uin"),
                ReadString(member, "nick"),
                ReadString(member, "cardName"),
                ReadInt32(member, "role"));
            result[memberId] = snapshot;
        }

        return result;
    }

    public static IReadOnlyList<RawQQMessage> ReadMessages(
        JsonElement response,
        string groupId,
        int page,
        int limit,
        IReadOnlyDictionary<string, QceMemberSnapshot>? members = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ValidatePositive(page, nameof(page));
        ValidatePositive(limit, nameof(limit));

        if (!TryGetDataObject(response, out var data) ||
            !TryGetProperty(data, "messages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RawQQMessage>();
        }

        var normalized = new List<RawQQMessage>();
        var offset = checked((page - 1) * limit);
        var index = 0;
        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var sourceMessageId = ReadString(message, "msgId");
            var memberId = FirstNonEmpty(
                ReadString(message, "senderUid"),
                ReadString(message, "fromUid"));
            QceMemberSnapshot? member = null;
            if (memberId is not null && members is not null) members.TryGetValue(memberId, out member);

            var nickname = FirstNonEmpty(
                ReadString(message, "sendMemberName"),
                ReadString(message, "sendNickName"),
                ReadString(message, "sendRemarkName"),
                member?.DisplayName);

            normalized.Add(new RawQQMessage(
                groupId,
                sourceMessageId,
                memberId,
                nickname,
                ReadTimestamp(message, "msgTime", "timeStamp"),
                BuildPayload(message))
            {
                Provider = "qce-direct",
                MessageSequence = ReadString(message, "msgSeq"),
                SourceOrder = checked(offset + index + 1),
                MessageKind = "qce-message",
            });
            index++;
        }

        return normalized;
    }

    private static JsonElementLike BuildPayload(JsonElement message)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetProperty(message, "elements", out var elements) ||
            elements.ValueKind != JsonValueKind.Array)
        {
            return new JsonElementLike(values);
        }

        var structuredElements = new List<object?>();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            var converted = ConvertJsonValue(element, omitPlainTextBranch: true);
            structuredElements.Add(converted);

            if (converted is not IReadOnlyDictionary<string, object?> elementFields) continue;
            foreach (var field in elementFields)
            {
                values[field.Key] = field.Value;
            }
        }

        values["elements"] = structuredElements;
        return new JsonElementLike(values);
    }

    private static object? ConvertJsonValue(JsonElement value, bool omitPlainTextBranch = false, string? propertyName = null)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer)) return integer;
                return value.GetDouble();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.GetBoolean();
            case JsonValueKind.String:
                var text = value.GetString();
                if (propertyName is not null &&
                    propertyName.Equals("bytesData", StringComparison.OrdinalIgnoreCase) &&
                    TryParseNestedJson(text, out var nestedDocument))
                {
                    using (nestedDocument)
                    {
                        return ConvertJsonValue(
                            nestedDocument.RootElement,
                            omitPlainTextBranch: true);
                    }
                }

                return text;
            case JsonValueKind.Array:
                return value.EnumerateArray()
                    .Select(item => ConvertJsonValue(item))
                    .ToArray();
            case JsonValueKind.Object:
                var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in value.EnumerateObject())
                {
                    if (omitPlainTextBranch && property.Name.Equals("textElement", StringComparison.OrdinalIgnoreCase)) continue;
                    fields[property.Name] = ConvertJsonValue(
                        property.Value,
                        omitPlainTextBranch: false,
                        property.Name);
                }

                return fields;
            default:
                return null;
        }
    }

    private static bool TryParseNestedJson(string? text, out JsonDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return true;
            }

            document.Dispose();
            document = null!;
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetDataArray(JsonElement response, string propertyName, out JsonElement array)
    {
        array = default;
        if (!TryGetProperty(response, "data", out var data)) return false;
        if (data.ValueKind == JsonValueKind.Array)
        {
            array = data;
            return true;
        }

        return TryGetProperty(data, propertyName, out array) && array.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetDataObject(JsonElement response, out JsonElement data)
    {
        return TryGetProperty(response, "data", out data) && data.ValueKind == JsonValueKind.Object;
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

    private static int? ReadInt32(JsonElement value, string name)
    {
        if (!TryGetProperty(value, name, out var property)) return null;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number)) return number;
        return int.TryParse(ReadString(value, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            var text = ReadString(value, name);
            if (text is null) continue;

            if (DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp))
            {
                return timestamp;
            }

            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixValue)) continue;
            try
            {
                return Math.Abs(unixValue) >= 100_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                    : DateTimeOffset.FromUnixTimeSeconds(unixValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Try the next verified timestamp field instead of fabricating a date.
            }
        }

        return null;
    }

    private static string? NormalizeString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
    }
}
