using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AlbumRobot.Sync.Core;

public sealed class BatchSyncClient(HttpClient httpClient, string? syncToken = null)
{
    public async Task<IReadOnlyList<SyncItemReceipt>> SubmitAsync(
        string groupId,
        IReadOnlyCollection<ShareCandidate> items,
        CancellationToken cancellationToken = default)
    {
        var payload = new BatchRequest(groupId, items.Select(ToWireCandidate).ToArray());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync/batch")
        {
            Content = JsonContent.Create(payload),
        };
        if (!string.IsNullOrWhiteSpace(syncToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", syncToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BatchResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Batch response was empty.");
        if (result.Status != "complete" || result.Items is null)
            throw new InvalidOperationException("Batch response was incomplete.");
        return result.Items.Select(item => new SyncItemReceipt(item.SourceMessageId, item.Status, item.Reason)).ToArray();
    }

    private static WireCandidate ToWireCandidate(ShareCandidate item) => new(
        item.GroupId,
        item.SourceMessageId,
        item.MemberId,
        item.MemberNickname,
        item.SharedAt,
        item.Album.AlbumId,
        item.Album.Title ?? "Untitled",
        item.Album.Artist ?? "Unknown artist",
        item.Album.CoverUrl,
        item.Album.CanonicalUrl,
        item.Origin);

    private sealed record BatchRequest(
        [property: JsonPropertyName("group_id")] string GroupId,
        [property: JsonPropertyName("items")] WireCandidate[] Items);

    private sealed record WireCandidate(
        [property: JsonPropertyName("group_id")] string GroupId,
        [property: JsonPropertyName("source_message_id")] string SourceMessageId,
        [property: JsonPropertyName("member_id")] string MemberId,
        [property: JsonPropertyName("member_nickname")] string MemberNickname,
        [property: JsonPropertyName("shared_at")] DateTimeOffset SharedAt,
        [property: JsonPropertyName("date_precision")] string DatePrecision,
        [property: JsonPropertyName("origin")] string Origin,
        [property: JsonPropertyName("album")] WireAlbum Album)
    {
        public WireCandidate(
            string groupId,
            string sourceMessageId,
            string memberId,
            string memberNickname,
            DateTimeOffset sharedAt,
            string albumId,
            string title,
            string artist,
            string? coverUrl,
            string neteaseUrl,
            string origin)
            : this(groupId, sourceMessageId, memberId, memberNickname, sharedAt, "second", origin, new WireAlbum(albumId, title, artist, coverUrl, neteaseUrl))
        {
        }
    }

    private sealed record WireAlbum(
        [property: JsonPropertyName("netease_album_id")] string NeteaseAlbumId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("artist")] string Artist,
        [property: JsonPropertyName("cover_url")] string? CoverUrl,
        [property: JsonPropertyName("netease_url")] string NeteaseUrl);

    private sealed record BatchResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("items")] BatchReceipt[]? Items);

    private sealed record BatchReceipt(
        [property: JsonPropertyName("source_message_id")] string SourceMessageId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("reason")] string? Reason);
}
