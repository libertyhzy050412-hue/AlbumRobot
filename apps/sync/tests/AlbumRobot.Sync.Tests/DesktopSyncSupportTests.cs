using System.Text.Json;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class DesktopSyncSupportTests
{
    [Fact]
    public void UsesTheProductionRemoteWorkerByDefault()
    {
        Assert.Equal("https://album.rocknrollliberty.dpdns.org", new SyncSettings().WorkerBaseUrl);
    }

    [Fact]
    public void ReadsQceGroupEnvelopeWithoutDependingOnConsoleEncoding()
    {
        using var document = JsonDocument.Parse("""
            {
              "success": true,
              "data": {
                "groups": [
                  { "groupCode": "group-1", "groupName": "Example Group", "memberCount": 12, "maxMember": 500, "avatarUrl": "https://example.invalid/avatar.png" }
                ],
                "totalCount": 1
              }
            }
            """);

        var group = Assert.Single(QceGroupNormalizer.ReadGroups(document.RootElement));

        Assert.Equal("group-1", group.GroupId);
        Assert.Equal("Example Group", group.DisplayName);
        Assert.Equal(12, group.MemberCount);
        Assert.Equal(500, group.MaxMember);
    }

    [Fact]
    public async Task SettingsRoundTripWithoutCredentialFields()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var settingsPath = Path.Combine(temporaryDirectory, "sync-settings.json");
        try
        {
            var store = new SyncSettingsStore(settingsPath);
            var expected = new SyncSettings
            {
                QceBaseUrl = "http://127.0.0.1:40653/",
                WorkerBaseUrl = "http://127.0.0.1:8787/",
                SelectedGroupId = "group-1",
                SelectedGroupName = "Example Group",
                LookbackDays = 14,
                InitialSyncCompleted = true,
                DataDirectory = Path.Combine(temporaryDirectory, "data"),
            };

            await store.SaveAsync(expected);
            var persisted = await File.ReadAllTextAsync(settingsPath);
            var loaded = await store.LoadAsync();

            Assert.DoesNotContain("accessToken", persisted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", persisted, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("http://127.0.0.1:40653", loaded.QceBaseUrl);
            Assert.Equal("http://127.0.0.1:8787", loaded.WorkerBaseUrl);
            Assert.Equal("group-1", loaded.SelectedGroupId);
            Assert.True(loaded.InitialSyncCompleted);
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task JsonImportStoresOnlyNeteaseCandidatesInPending()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var jsonPath = Path.Combine(temporaryDirectory, "export.json");
        var databasePath = Path.Combine(temporaryDirectory, "pending.sqlite");
        try
        {
            await File.WriteAllTextAsync(jsonPath, """
                [
                  {
                    "id": "json-message-1",
                    "seq": "json-seq-1",
                    "time": "2026-08-14T15:42:17.000Z",
                    "sender": { "uid": "member-1", "name": "Example Member" },
                    "type": "json",
                    "content": {
                      "elements": [
                        { "type": "json", "data": { "content": "{\"meta\":{\"news\":{\"desc\":\"Example Artist\",\"jumpUrl\":\"https://music.163.com/album/123456/?from=test\",\"title\":\"Example Album\",\"preview\":\"https://example.invalid/cover.jpg\"}}}" } }
                      ]
                    }
                  },
                  {
                    "id": "json-message-2",
                    "seq": "json-seq-2",
                    "time": "2026-08-14T15:43:17.000Z",
                    "sender": { "uid": "member-2", "name": "Other Member" },
                    "type": "json",
                    "content": {
                      "elements": [
                        { "type": "json", "data": { "content": "{\"meta\":{\"news\":{\"jumpUrl\":\"https://example.invalid/album/987\",\"title\":\"Not Netease\"}}}" } }
                      ]
                    }
                  }
                ]
                """);

            await using (var pendingStore = new PendingStore(databasePath))
            {
                await pendingStore.InitializeAsync();
                var importer = new QceJsonImportService(pendingStore);

                var result = await importer.ImportFileAsync(jsonPath, "group-1");
                var pending = await pendingStore.ListPendingAsync();

                Assert.Equal(2, result.MessagesRead);
                Assert.Equal(1, result.CandidatesDetected);
                var candidate = Assert.Single(pending);
                Assert.Equal("json-message-1", candidate.SourceMessageId);
                Assert.Equal("123456", candidate.Album.AlbumId);
                Assert.Equal("Example Album", candidate.Album.Title);
                Assert.Equal("Example Artist", candidate.Album.Artist);
            }
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task RuntimeCanStartInLocalJsonOnlyMode()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var settings = new SyncSettings
            {
                DataDirectory = temporaryDirectory,
            };

            await using (var runtime = await SyncRuntime.CreateAsync(settings, qceAccessToken: null))
            {
                Assert.Null(runtime.QceClient);
                Assert.Null(runtime.Scanner);
                Assert.NotNull(runtime.JsonImporter);
                Assert.True(File.Exists(Path.Combine(temporaryDirectory, "pending.sqlite")));
            }
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "albumrobot-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
