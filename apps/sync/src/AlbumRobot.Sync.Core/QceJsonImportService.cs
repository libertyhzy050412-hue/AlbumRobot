using System.Text.Json;

namespace AlbumRobot.Sync.Core;

public sealed record QceJsonImportResult(int MessagesRead, int CandidatesDetected);

public sealed class QceJsonImportService
{
    private readonly PendingStore pendingStore;

    public QceJsonImportService(PendingStore pendingStore)
    {
        ArgumentNullException.ThrowIfNull(pendingStore);
        this.pendingStore = pendingStore;
    }

    public async Task<QceJsonImportResult> ImportFileAsync(
        string filePath,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var messages = QceJsonExportNormalizer.ReadMessages(document.RootElement, groupId);
            var candidatesDetected = 0;
            foreach (var message in messages)
            {
                if (!TryCreateCandidate(message, out var candidate)) continue;
                await pendingStore.UpsertAsync(candidate, cancellationToken);
                candidatesDetected++;
            }

            return new QceJsonImportResult(messages.Count, candidatesDetected);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The selected QCE JSON file is invalid.", exception);
        }
        catch (IOException exception)
        {
            throw new IOException("The selected QCE JSON file could not be read.", exception);
        }
    }

    private static bool TryCreateCandidate(RawQQMessage message, out ShareCandidate candidate)
    {
        candidate = null!;
        if (string.IsNullOrWhiteSpace(message.SourceMessageId) ||
            string.IsNullOrWhiteSpace(message.MemberId) ||
            string.IsNullOrWhiteSpace(message.MemberNickname) ||
            message.SharedAt is null)
        {
            return false;
        }

        var album = NeteaseAlbumDetector.Detect(message.Payload);
        if (album is null) return false;
        candidate = new ShareCandidate(
            message.GroupId,
            message.SourceMessageId,
            message.MemberId,
            message.MemberNickname,
            message.SharedAt.Value,
            album,
            "detected");
        return true;
    }
}
