using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class QceDirectClientTests
{
    [Fact]
    public async Task SendsVerifiedAuthHeadersWithoutPuttingTokenInUrl()
    {
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true, data = new { groups = Array.Empty<object>() } }),
            });
        }));
        var client = new QceDirectClient(
            httpClient,
            new QceDirectOptions(new Uri("http://127.0.0.1:40654"), "local-test-token"));

        using var response = await client.GetGroupsPageAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer local-test-token", capturedRequest!.Headers.Authorization!.ToString());
        Assert.Equal("local-test-token", capturedRequest.Headers.GetValues("X-Access-Token").Single());
        Assert.DoesNotContain("local-test-token", capturedRequest.RequestUri!.Query);
        Assert.Equal("/api/groups", capturedRequest.RequestUri.AbsolutePath);
        Assert.Equal("True", response.RootElement.GetProperty("success").GetBoolean().ToString());
    }

    [Fact]
    public async Task SendsTheQceMessageFetchShapeAndPreservesPeerJson()
    {
        string? requestJson = null;
        using var httpClient = new HttpClient(new StubHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true, data = new { messages = Array.Empty<object>() } }),
            };
        }));
        var client = new QceDirectClient(
            httpClient,
            new QceDirectOptions(new Uri("http://127.0.0.1:40654"), "local-test-token"));
        using var peerDocument = JsonDocument.Parse("{\"chatType\":2,\"peerUid\":\"group-001\"}");

        using var response = await client.FetchMessagesAsync(
            peerDocument.RootElement,
            page: 2,
            limit: 50,
            new QceMessageFilter(1_700_000_000_000, 1_700_100_000_000));

        Assert.NotNull(requestJson);
        using var requestDocument = JsonDocument.Parse(requestJson!);
        var root = requestDocument.RootElement;
        Assert.Equal(2, root.GetProperty("peer").GetProperty("chatType").GetInt32());
        Assert.Equal("group-001", root.GetProperty("peer").GetProperty("peerUid").GetString());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(50, root.GetProperty("limit").GetInt32());
        Assert.Equal(1_700_000_000_000, root.GetProperty("filter").GetProperty("startTime").GetInt64());
        Assert.Equal(1_700_100_000_000, root.GetProperty("filter").GetProperty("endTime").GetInt64());
        Assert.True(response.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task DoesNotCopyQceErrorBodyIntoException()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(new { message = "may contain private detail" }),
        })));
        var client = new QceDirectClient(
            httpClient,
            new QceDirectOptions(new Uri("http://127.0.0.1:40654"), "local-test-token"));

        var exception = await Assert.ThrowsAsync<QceApiException>(() => client.GetGroupsPageAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.DoesNotContain("private detail", exception.Message);
        Assert.DoesNotContain("local-test-token", exception.Message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
