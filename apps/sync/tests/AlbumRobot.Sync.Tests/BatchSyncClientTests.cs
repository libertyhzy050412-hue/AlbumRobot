using System.Net;
using System.Net.Http.Json;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class BatchSyncClientTests
{
    [Fact]
    public async Task SendsNormalizedCandidateAndReadsReceipt()
    {
        string? requestJson = null;
        string? authorization = null;
        using var httpClient = new HttpClient(new StubHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "complete",
                    items = new[] { new { source_message_id = "message-003", status = "accepted" } },
                }),
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:8787"),
        };
        var client = new BatchSyncClient(httpClient, "example-sync-token");

        var receipts = await client.SubmitAsync("group-001", [Candidate()]);

        Assert.Single(receipts);
        Assert.Equal("accepted", receipts[0].Status);
        Assert.Equal("Bearer example-sync-token", authorization);
        Assert.Contains("source_message_id", requestJson);
        Assert.DoesNotContain("raw_payload", requestJson);
    }

    private static ShareCandidate Candidate() => new(
        "group-001",
        "message-003",
        "member-001",
        "Example Member",
        DateTimeOffset.Parse("2026-01-02T12:34:56+08:00"),
        new NeteaseAlbum("123456", "Example Album", "Example Artist", null, NeteaseAlbumDetector.CanonicalUrl("123456")));

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
