using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed record QceGroupSnapshot(
    string GroupId,
    string GroupName,
    int MemberCount,
    int MaxMember,
    string? AvatarUrl)
{
    public string DisplayName => string.IsNullOrWhiteSpace(GroupName) ? GroupId : GroupName;
}

public static class QceGroupNormalizer
{
    public static IReadOnlyList<QceGroupSnapshot> ReadGroups(JsonElement response)
    {
        if (!TryGetProperty(response, "data", out var data) ||
            !TryGetProperty(data, "groups", out var groups) ||
            groups.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<QceGroupSnapshot>();
        }

        return groups.EnumerateArray()
            .Where(group => group.ValueKind == JsonValueKind.Object)
            .Select(group => new QceGroupSnapshot(
                ReadString(group, "groupCode") ?? string.Empty,
                ReadString(group, "groupName") ?? string.Empty,
                ReadInt32(group, "memberCount"),
                ReadInt32(group, "maxMember"),
                ReadString(group, "avatarUrl")))
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId))
            .ToArray();
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
            JsonValueKind.String => string.IsNullOrWhiteSpace(property.GetString()) ? null : property.GetString()!.Trim(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    private static int ReadInt32(JsonElement value, string name)
    {
        if (!TryGetProperty(value, name, out var property)) return 0;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number)) return number;
        return int.TryParse(ReadString(value, name), out var parsed) ? parsed : 0;
    }
}
