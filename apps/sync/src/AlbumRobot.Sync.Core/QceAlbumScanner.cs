using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed record QceScanResult(
    int PagesFetched,
    int MessagesSeen,
    int CandidatesDetected,
    bool ReachedEnd);

/// <summary>
/// Local vertical-slice orchestrator: QCE -> RawQQMessage -> Detector -> SQLite.
/// It never sends a RawQQMessage to the network client.
/// </summary>
public sealed class QceAlbumScanner
{
    private readonly QceDirectClient qceClient;
    private readonly PendingStore pendingStore;

    public QceAlbumScanner(QceDirectClient qceClient, PendingStore pendingStore)
    {
        ArgumentNullException.ThrowIfNull(qceClient);
        ArgumentNullException.ThrowIfNull(pendingStore);
        this.qceClient = qceClient;
        this.pendingStore = pendingStore;
    }

    public async Task<QceScanResult> ScanGroupAsync(
        string groupId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (endTime < startTime) throw new ArgumentException("End time must not precede start time.", nameof(endTime));
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        using var memberResponse = await qceClient.GetGroupMembersAsync(groupId, cancellationToken: cancellationToken);
        EnsureSuccessful(memberResponse.RootElement, "group members");
        var members = QceMessageNormalizer.ReadMembers(memberResponse.RootElement);

        using var peerDocument = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            chatType = 2,
            peerUid = groupId,
        }));

        var page = 1;
        var pagesFetched = 0;
        var messagesSeen = 0;
        var candidatesDetected = 0;
        var reachedEnd = true;
        var filter = new QceMessageFilter(
            startTime.ToUnixTimeMilliseconds(),
            endTime.ToUnixTimeMilliseconds());

        while (true)
        {
            using var messageResponse = await qceClient.FetchMessagesAsync(
                peerDocument.RootElement,
                page,
                pageSize,
                filter,
                cancellationToken);
            EnsureSuccessful(messageResponse.RootElement, "messages");

            var rawMessages = QceMessageNormalizer.ReadMessages(
                messageResponse.RootElement,
                groupId,
                page,
                pageSize,
                members);
            pagesFetched++;
            messagesSeen += rawMessages.Count;

            foreach (var rawMessage in rawMessages)
            {
                if (!TryCreateCandidate(rawMessage, out var candidate)) continue;
                await pendingStore.UpsertAsync(candidate, cancellationToken);
                candidatesDetected++;
            }

            var hasNext = ReadBoolean(messageResponse.RootElement, "data", "hasNext");
            if (!hasNext || rawMessages.Count == 0)
            {
                reachedEnd = true;
                break;
            }

            reachedEnd = false;
            page++;
        }

        return new QceScanResult(pagesFetched, messagesSeen, candidatesDetected, reachedEnd);
    }

    private static bool TryCreateCandidate(RawQQMessage rawMessage, out ShareCandidate candidate)
    {
        candidate = null!;
        if (string.IsNullOrWhiteSpace(rawMessage.SourceMessageId) ||
            string.IsNullOrWhiteSpace(rawMessage.MemberId) ||
            string.IsNullOrWhiteSpace(rawMessage.MemberNickname) ||
            rawMessage.SharedAt is null)
        {
            return false;
        }

        var album = NeteaseAlbumDetector.Detect(rawMessage.Payload);
        if (album is null) return false;

        candidate = new ShareCandidate(
            rawMessage.GroupId,
            rawMessage.SourceMessageId,
            rawMessage.MemberId,
            rawMessage.MemberNickname,
            rawMessage.SharedAt.Value,
            album,
            "detected");
        return true;
    }

    private static void EnsureSuccessful(JsonElement response, string operation)
    {
        if (!response.TryGetProperty("success", out var success) ||
            success.ValueKind != JsonValueKind.True)
        {
            throw new InvalidDataException($"QCE {operation} response was not successful.");
        }
    }

    private static bool ReadBoolean(JsonElement response, string objectName, string propertyName)
    {
        if (!response.TryGetProperty(objectName, out var data) ||
            !data.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.True;
    }
}
