using System.Collections;
using System.Text.RegularExpressions;

namespace AlbumRobot.Sync.Core;

public sealed record RawQQMessage(
    string GroupId,
    string? SourceMessageId,
    string? MemberId,
    string? MemberNickname,
    DateTimeOffset? SharedAt,
    JsonElementLike Payload)
{
    public string Provider { get; init; } = "qce-direct";

    public string? MessageSequence { get; init; }

    public int? SourceOrder { get; init; }

    public string MessageKind { get; init; } = "unknown";
}

public sealed record NeteaseAlbum(
    string AlbumId,
    string? Title,
    string? Artist,
    string? CoverUrl,
    string CanonicalUrl);

public sealed record ShareCandidate(
    string GroupId,
    string SourceMessageId,
    string MemberId,
    string MemberNickname,
    DateTimeOffset SharedAt,
    NeteaseAlbum Album,
    string Origin = "detected");

public sealed record JsonElementLike(IReadOnlyDictionary<string, object?> Values)
{
    public object? this[string key] => Values.TryGetValue(key, out var value) ? value : null;

    public IEnumerable<KeyValuePair<string, object?>> Fields => Values;
}

public static partial class NeteaseAlbumDetector
{
    private static readonly string[] UrlKeys = ["url", "album_url", "netease_url", "jumpUrl", "link"];
    private static readonly string[] IdKeys = ["album_id", "albumId", "netease_album_id", "id"];

    public static NeteaseAlbum? Detect(JsonElementLike payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        foreach (var key in IdKeys)
        {
            if (TryNormalizeAlbumId(payload[key], out var albumId))
            {
                return CreateAlbum(payload, albumId);
            }
        }

        foreach (var field in EnumerateFields(payload.Values))
        {
            if (field.Key.Equals("id", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IdKeys.Contains(field.Key, StringComparer.OrdinalIgnoreCase)) continue;
            if (TryNormalizeAlbumId(field.Value, out var albumId))
            {
                return CreateAlbum(payload, albumId);
            }
        }

        foreach (var key in UrlKeys)
        {
            if (TryExtractAlbumId(payload[key], out var albumId))
            {
                return CreateAlbum(payload, albumId);
            }
        }

        foreach (var value in EnumerateValues(payload.Values))
        {
            if (TryExtractAlbumId(value, out var albumId))
            {
                return CreateAlbum(payload, albumId);
            }
        }

        return null;
    }

    public static bool TryExtractAlbumId(object? value, out string albumId)
    {
        albumId = string.Empty;
        if (value is not string text) return false;
        var match = AlbumIdRegex().Match(text);
        if (!match.Success) return false;
        albumId = match.Groups[1].Value;
        return true;
    }

    public static bool TryNormalizeAlbumId(object? value, out string albumId)
    {
        albumId = string.Empty;
        if (value is not string text || !Regex.IsMatch(text.Trim(), "^\\d+$")) return false;
        albumId = text.Trim();
        return true;
    }

    public static string CanonicalUrl(string albumId) => $"https://music.163.com/#/album?id={albumId}";

    private static string? GetString(JsonElementLike payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload[key] is string value && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        foreach (var field in EnumerateFields(payload.Values))
        {
            if (!keys.Contains(field.Key, StringComparer.OrdinalIgnoreCase)) continue;
            if (field.Value is string value && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }

    private static NeteaseAlbum CreateAlbum(JsonElementLike payload, string albumId)
    {
        return new NeteaseAlbum(
            albumId,
            CleanCardText(GetString(payload, "title", "summary")),
            CleanCardText(GetString(payload, "artist", "desc", "description")),
            GetString(payload, "cover_url", "cover", "preview"),
            CanonicalUrl(albumId));
    }

    private static string? CleanCardText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        foreach (var prefix in new[] { "[分享]", "专辑：", "专辑:", "歌手：", "歌手:" })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateFields(object? value)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var field in readOnlyDictionary)
            {
                yield return field;
                foreach (var nestedField in EnumerateFields(field.Value)) yield return nestedField;
            }

            yield break;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key) continue;
                yield return new KeyValuePair<string, object?>(key, entry.Value);
                foreach (var nestedField in EnumerateFields(entry.Value)) yield return nestedField;
            }

            yield break;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                foreach (var nestedField in EnumerateFields(item)) yield return nestedField;
            }
        }
    }

    private static IEnumerable<object?> EnumerateValues(object? value)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var field in readOnlyDictionary)
            {
                yield return field.Value;
                foreach (var nestedValue in EnumerateValues(field.Value)) yield return nestedValue;
            }

            yield break;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return entry.Value;
                foreach (var nestedValue in EnumerateValues(entry.Value)) yield return nestedValue;
            }

            yield break;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
                foreach (var nestedValue in EnumerateValues(item)) yield return nestedValue;
            }
        }
    }

    [GeneratedRegex(@"(?:music\.163\.com|163cn\.tv)[^\r\n]*?(?:album\?id=|/album/)(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AlbumIdRegex();
}
