using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class NeteaseAlbumDetectorTests
{
    [Fact]
    public void DetectsStructuredAlbumIdBeforeUrl()
    {
        var payload = new JsonElementLike(new Dictionary<string, object?>
        {
            ["album_id"] = "123456",
            ["url"] = "https://music.163.com/#/album?id=999999",
            ["title"] = "Loveless",
            ["artist"] = "My Bloody Valentine",
        });

        var album = NeteaseAlbumDetector.Detect(payload);

        Assert.NotNull(album);
        Assert.Equal("123456", album!.AlbumId);
        Assert.Equal("https://music.163.com/#/album?id=123456", album.CanonicalUrl);
    }

    [Fact]
    public void RejectsTextOnlyGuess()
    {
        var payload = new JsonElementLike(new Dictionary<string, object?>
        {
            ["text"] = "推荐一张很棒的专辑 123456",
        });

        Assert.Null(NeteaseAlbumDetector.Detect(payload));
    }

    [Fact]
    public void ExtractsAlbumIdFromSupportedUrl()
    {
        Assert.True(NeteaseAlbumDetector.TryExtractAlbumId("https://music.163.com/#/album?id=123456", out var albumId));
        Assert.Equal("123456", albumId);
    }
}
