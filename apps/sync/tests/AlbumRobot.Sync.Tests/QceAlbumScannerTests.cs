using System.Net;
using System.Text;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class QceAlbumScannerTests
{
    [Fact]
    public async Task ScansQceIntoLocalPendingWithoutUploadingRawMessage()
    {
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/members", StringComparison.Ordinal))
            {
                return Json("""
                    { "success": true, "data": [ { "uid": "member-example", "nick": "Example Nick", "cardName": "Example Card" } ] }
                    """);
            }

            return Json("""
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
                          { "elementType": 10, "arkElement": { "title": "Example Album", "artist": "Example Artist", "url": "https://music.163.com/#/album?id=123456" } }
                        ]
                      }
                    ],
                    "hasNext": false
                  }
                }
                """);
        }));
        var qce = new QceDirectClient(
            httpClient,
            new QceDirectOptions(new Uri("http://127.0.0.1:40653"), "local-test-token"));
        await using var store = new PendingStore(":memory:");
        await store.InitializeAsync();
        var scanner = new QceAlbumScanner(qce, store);

        var result = await scanner.ScanGroupAsync(
            "group-example",
            DateTimeOffset.Parse("2026-08-15T00:00:00+08:00"),
            DateTimeOffset.Parse("2026-08-15T23:59:59+08:00"));

        Assert.Equal(1, result.PagesFetched);
        Assert.Equal(1, result.MessagesSeen);
        Assert.Equal(1, result.CandidatesDetected);
        var pending = Assert.Single(await store.ListPendingAsync());
        Assert.Equal("123456", pending.Album.AlbumId);
        Assert.Equal("message-example", pending.SourceMessageId);
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
