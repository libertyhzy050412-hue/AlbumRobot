using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed class QceAccessTokenProvider
{
    public QceAccessTokenProvider(string? securityPath = null)
    {
        SecurityPath = securityPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".qq-chat-exporter",
            "security.json");
    }

    public string SecurityPath { get; }

    public bool TryRead(out string accessToken)
    {
        accessToken = string.Empty;
        if (!File.Exists(SecurityPath)) return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(SecurityPath));
            if (!document.RootElement.TryGetProperty("accessToken", out var token) ||
                token.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = token.GetString();
            if (string.IsNullOrWhiteSpace(value)) return false;
            accessToken = value;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
