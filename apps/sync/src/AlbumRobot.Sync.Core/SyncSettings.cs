using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed record SyncSettings
{
    public string QceBaseUrl { get; init; } = "http://127.0.0.1:40653";

    public string WorkerBaseUrl { get; init; } = "https://album.rocknrollliberty.dpdns.org";

    public string? SelectedGroupId { get; init; }

    public string? SelectedGroupName { get; init; }

    public int LookbackDays { get; init; } = 30;

    public int PageSize { get; init; } = 100;

    public bool InitialSyncCompleted { get; init; }

    public string DataDirectory { get; init; } = DefaultDataDirectory;

    public static string DefaultDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AlbumRobot",
        "data");

    public SyncSettings Normalize() => this with
    {
        QceBaseUrl = QceBaseUrl.Trim().TrimEnd('/'),
        WorkerBaseUrl = WorkerBaseUrl.Trim().TrimEnd('/'),
        SelectedGroupId = NormalizeOptional(SelectedGroupId),
        SelectedGroupName = NormalizeOptional(SelectedGroupName),
        DataDirectory = string.IsNullOrWhiteSpace(DataDirectory) ? DefaultDataDirectory : DataDirectory.Trim(),
    };

    public void Validate()
    {
        var normalized = Normalize();
        ValidateHttpUrl(normalized.QceBaseUrl, nameof(QceBaseUrl));
        ValidateHttpUrl(normalized.WorkerBaseUrl, nameof(WorkerBaseUrl));
        if (LookbackDays is < 1 or > 3650) throw new ArgumentOutOfRangeException(nameof(LookbackDays));
        if (PageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(PageSize));
        if (string.IsNullOrWhiteSpace(normalized.DataDirectory)) throw new ArgumentException("Data directory is required.", nameof(DataDirectory));
    }

    private static void ValidateHttpUrl(string value, string parameterName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A HTTP(S) URL is required.", parameterName);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SyncSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public SyncSettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AlbumRobot",
            "sync-settings.json");
    }

    public string SettingsPath { get; }

    public async Task<SyncSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath)) return new SyncSettings();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
            return (JsonSerializer.Deserialize<SyncSettings>(json, JsonOptions) ?? new SyncSettings()).Normalize();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Local Sync settings are not valid JSON.", exception);
        }
    }

    public async Task SaveAsync(SyncSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        normalized.Validate();

        var directory = Path.GetDirectoryName(SettingsPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Settings directory is unavailable.");
        Directory.CreateDirectory(directory);

        var temporaryPath = SettingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
