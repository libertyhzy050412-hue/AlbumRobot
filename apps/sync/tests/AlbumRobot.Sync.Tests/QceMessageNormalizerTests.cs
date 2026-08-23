using System.Text.Json;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class QceMessageNormalizerTests
{
    [Fact]
    public void ReadsVerifiedMemberAndMessageEnvelopeFields()
    {
        using var document = JsonDocument.Parse("""
            {
              "success": true,
              "data": [
                { "uid": "member-example", "uin": "uin-example", "nick": "Example Nick", "cardName": "Example Card", "role": 2 }
              ]
            }
            """);

        var members = QceMessageNormalizer.ReadMembers(document.RootElement);

        Assert.Single(members);
        Assert.Equal("Example Card", members["member-example"].DisplayName);
    }

    [Fact]
    public void NormalizesStructuredElementAndOmitsPlainTextBranch()
    {
        using var document = JsonDocument.Parse("""
            {
              "success": true,
              "data": {
                "messages": [
                  {
                    "msgId": "message-example",
                    "msgSeq": "sequence-example",
                    "senderUid": "member-example",
                    "sendMemberName": "Example Member",
                    "msgTime": "2026-08-15T10:20:30+08:00",
                    "elements": [
                      { "elementType": 10, "arkElement": { "title": "Example Album", "artist": "Example Artist", "url": "https://music.163.com/#/album?id=123456" } },
                      { "elementType": 1, "textElement": { "content": "ordinary chat text" } }
                    ]
                  }
                ],
                "hasNext": false
              }
            }
            """);

        var messages = QceMessageNormalizer.ReadMessages(document.RootElement, "group-example", 1, 100);

        var message = Assert.Single(messages);
        Assert.Equal("message-example", message.SourceMessageId);
        Assert.Equal("sequence-example", message.MessageSequence);
        Assert.Equal("member-example", message.MemberId);
        Assert.Equal("Example Member", message.MemberNickname);
        Assert.Equal(1, message.SourceOrder);
        Assert.Null(message.Payload["textElement"]);

        var album = NeteaseAlbumDetector.Detect(message.Payload);
        Assert.NotNull(album);
        Assert.Equal("123456", album!.AlbumId);
        Assert.Equal("Example Album", album.Title);
        Assert.Equal("Example Artist", album.Artist);
    }

    [Fact]
    public void ParsesDirectArkBytesDataCardMetadata()
    {
        using var document = JsonDocument.Parse("""
            {
              "success": true,
              "data": {
                "messages": [
                  {
                    "msgId": "message-bytes-data",
                    "senderUid": "member-example",
                    "sendMemberName": "Example Member",
                    "msgTime": "2026-08-15T10:20:30+08:00",
                    "elements": [
                      {
                        "elementType": 10,
                        "arkElement": {
                          "bytesData": "{\"meta\":{\"news\":{\"title\":\"Bytes Album\",\"desc\":\"Bytes Artist\",\"jumpUrl\":\"https://music.163.com/#/album?id=654321\",\"preview\":\"https://example.invalid/bytes-cover.jpg\"}}}"
                        }
                      }
                    ]
                  }
                ],
                "hasNext": false
              }
            }
            """);

        var message = Assert.Single(
            QceMessageNormalizer.ReadMessages(document.RootElement, "group-example", 1, 100));

        var album = NeteaseAlbumDetector.Detect(message.Payload);

        Assert.NotNull(album);
        Assert.Equal("654321", album!.AlbumId);
        Assert.Equal("Bytes Album", album.Title);
        Assert.Equal("Bytes Artist", album.Artist);
        Assert.Equal("https://example.invalid/bytes-cover.jpg", album.CoverUrl);
    }

    [Fact]
    public void KeepsMessagesWithMissingOptionalFieldsWithoutInventingValues()
    {
        using var document = JsonDocument.Parse("""
            {
              "data": {
                "messages": [
                  { "msgSeq": "sequence-only", "elements": [] }
                ]
              }
            }
            """);

        var message = Assert.Single(QceMessageNormalizer.ReadMessages(document.RootElement, "group-example", 2, 10));

        Assert.Null(message.SourceMessageId);
        Assert.Null(message.MemberId);
        Assert.Null(message.MemberNickname);
        Assert.Null(message.SharedAt);
        Assert.Equal(11, message.SourceOrder);
    }
}
