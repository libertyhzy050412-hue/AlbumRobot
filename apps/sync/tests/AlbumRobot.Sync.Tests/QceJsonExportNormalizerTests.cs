using System.Text.Json;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class QceJsonExportNormalizerTests
{
    [Fact]
    public void NormalizesJsonExportAndRejectsNonNeteaseAlbum()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "message-example",
              "seq": "sequence-example",
              "timestamp": 1786722137000,
              "time": "2026-08-14T15:42:17.000Z",
              "sender": { "uid": "member-example", "name": "Example Member", "title": "Example Card" },
              "type": "json",
              "content": {
                "elements": [
                  { "type": "json", "data": { "content": "{\"meta\":{\"news\":{\"desc\":\"Example Artist\",\"jumpUrl\":\"https://example.invalid/album?id=example\",\"title\":\"Example Album\"}}}" } }
                ]
              }
            }
            """);

        var message = Assert.Single(QceJsonExportNormalizer.ReadMessages(document.RootElement, "group-example"));

        Assert.Equal("qce-json", message.Provider);
        Assert.Equal("message-example", message.SourceMessageId);
        Assert.Equal("sequence-example", message.MessageSequence);
        Assert.Equal("member-example", message.MemberId);
        Assert.Equal("Example Card", message.MemberNickname);
        Assert.Null(NeteaseAlbumDetector.Detect(message.Payload));
    }

    [Fact]
    public void DetectsNeteaseAlbumInsideNestedJsonContentAndRedactsToken()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "message-example",
              "seq": "sequence-example",
              "time": "2026-08-14T15:42:17.000Z",
              "sender": { "uid": "member-example", "nickname": "Example Member" },
              "type": "json",
              "content": {
                "elements": [
                  { "type": "json", "data": { "title": "[分享]专辑：Example Album", "description": "歌手：Example Artist", "preview": "https://example.invalid/cover.jpg", "content": "{\"config\":{\"token\":\"synthetic-secret\"},\"meta\":{\"news\":{\"desc\":\"歌手：Example Artist\",\"jumpUrl\":\"https://music.163.com/#/album?id=123456\",\"title\":\"专辑：Example Album\",\"preview\":\"https://example.invalid/cover.jpg\"}}}" } }
                ]
              }
            }
            """);

        var message = Assert.Single(QceJsonExportNormalizer.ReadMessages(document.RootElement, "group-example"));
        var album = NeteaseAlbumDetector.Detect(message.Payload);
        var payloadJson = JsonSerializer.Serialize(message.Payload.Values);

        Assert.NotNull(album);
        Assert.Equal("123456", album!.AlbumId);
        Assert.Equal("Example Album", album.Title);
        Assert.Equal("Example Artist", album.Artist);
        Assert.Equal("https://example.invalid/cover.jpg", album.CoverUrl);
        Assert.DoesNotContain("synthetic-secret", payloadJson);
    }

    [Fact]
    public void RejectsGenericQqElementIdWithoutNeteaseEvidence()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "message-example",
              "seq": "sequence-example",
              "time": "2026-08-14T15:42:17.000Z",
              "sender": { "uid": "member-example", "nickname": "Example Member" },
              "type": "face",
              "content": {
                "elements": [
                  { "type": "face", "data": { "id": "123456", "faceType": "synthetic-face", "name": "Synthetic element" } }
                ]
              }
            }
            """);

        var message = Assert.Single(QceJsonExportNormalizer.ReadMessages(document.RootElement, "group-example"));

        Assert.Null(NeteaseAlbumDetector.Detect(message.Payload));
    }
}
