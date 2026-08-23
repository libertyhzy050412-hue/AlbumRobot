using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class SyncTokenCredentialStoreTests
{
    [Fact]
    public void UsesTheSameCredentialTargetForEquivalentWorkerUrls()
    {
        var withSlash = SyncTokenCredentialStore.BuildTarget("https://album.example.test/");
        var withoutSlash = SyncTokenCredentialStore.BuildTarget("https://album.example.test");

        Assert.Equal(withSlash, withoutSlash);
        Assert.StartsWith("AlbumRobot.SyncToken.v1.", withSlash, StringComparison.Ordinal);
    }

    [Fact]
    public void SeparatesCredentialTargetsByWorkerUrl()
    {
        var first = SyncTokenCredentialStore.BuildTarget("https://album.example.test");
        var second = SyncTokenCredentialStore.BuildTarget("https://other.example.test");

        Assert.NotEqual(first, second);
    }

}
