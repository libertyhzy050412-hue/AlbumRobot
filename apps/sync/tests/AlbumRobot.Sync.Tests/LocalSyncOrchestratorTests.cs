using System.Net;
using System.Text;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class LocalSyncOrchestratorTests
{
    [Fact]
    public async Task SendsOnlyNormalizedCandidateAndAppliesAcceptedReceipt()
    {
        using var qceHttpClient = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/members", StringComparison.Ordinal))
            {
                return Task.FromResult(Json("{ \"success\": true, \"data\": [ { \"uid\": \"member-example\", \"nick\": \"Example Member\" } ] }"));
            }

            return Task.FromResult(Json("""
                {
                  "success": true,
                  "data": {
                    "messages": [
                      {
                        "msgId": "message-example",
                        "senderUid": "member-example",
                        "sendMemberName": "Example Member",
                        "msgTime": "2026-08-15T10:20:30+08:00",
                        "elements": [ { "elementType": 10, "arkElement": { "url": "https://music.163.com/#/album?id=123456", "title": "Example Album", "artist": "Example Artist" } } ]
                      }
                    ],
                    "hasNext": false
                  }
                }
                """));
        }));
        using var batchHttpClient = new HttpClient(new StubHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("source_message_id", body);
            Assert.DoesNotContain("elements", body);
            Assert.DoesNotContain("raw_payload", body);
            return Json("{ \"status\": \"complete\", \"items\": [ { \"source_message_id\": \"message-example\", \"status\": \"accepted\" } ] }");
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:8787"),
        };

        var qce = new QceDirectClient(
            qceHttpClient,
            new QceDirectOptions(new Uri("http://127.0.0.1:40653"), "local-test-token"));
        await using var store = new PendingStore(":memory:");
        await store.InitializeAsync();
        var orchestrator = new LocalSyncOrchestrator(
            new QceAlbumScanner(qce, store),
            store,
            new BatchSyncClient(batchHttpClient));

        var result = await orchestrator.RunGroupAsync(
            "group-example",
            DateTimeOffset.Parse("2026-08-15T00:00:00+08:00"),
            DateTimeOffset.Parse("2026-08-15T23:59:59+08:00"));

        Assert.Equal(1, result.Submitted);
        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Duplicates);
        Assert.Empty(await store.ListPendingAsync());
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
