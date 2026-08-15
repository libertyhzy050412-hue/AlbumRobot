namespace AlbumRobot.Sync.Core;

public sealed record LocalSyncRunResult(
    QceScanResult Scan,
    int Submitted,
    int Accepted,
    int Duplicates,
    int Invalid);

public sealed record LocalSyncSubmissionResult(
    int Submitted,
    int Accepted,
    int Duplicates,
    int Invalid);

/// <summary>
/// Completes the local queue -> Batch API part of the vertical slice.
/// Only ShareCandidate values are handed to BatchSyncClient.
/// </summary>
public sealed class LocalSyncOrchestrator
{
    private readonly QceAlbumScanner? scanner;
    private readonly PendingStore pendingStore;
    private readonly BatchSyncClient batchClient;

    public LocalSyncOrchestrator(
        QceAlbumScanner? scanner,
        PendingStore pendingStore,
        BatchSyncClient batchClient)
    {
        ArgumentNullException.ThrowIfNull(pendingStore);
        ArgumentNullException.ThrowIfNull(batchClient);
        this.scanner = scanner;
        this.pendingStore = pendingStore;
        this.batchClient = batchClient;
    }

    public async Task<LocalSyncRunResult> RunGroupAsync(
        string groupId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (scanner is null) throw new InvalidOperationException("The QCE Direct provider is unavailable.");
        await pendingStore.RecoverSubmittingAsync(cancellationToken);

        var scan = await scanner.ScanGroupAsync(
            groupId,
            startTime,
            endTime,
            pageSize,
            cancellationToken);
        var submission = await SubmitPendingAsync(groupId, cancellationToken);
        return new LocalSyncRunResult(
            scan,
            submission.Submitted,
            submission.Accepted,
            submission.Duplicates,
            submission.Invalid);
    }

    public async Task<LocalSyncSubmissionResult> SubmitPendingAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        await pendingStore.RecoverSubmittingAsync(cancellationToken);

        var pending = (await pendingStore.ListPendingAsync(cancellationToken: cancellationToken))
            .Where(item => item.GroupId == groupId)
            .ToArray();
        if (pending.Length == 0)
        {
            return new LocalSyncSubmissionResult(0, 0, 0, 0);
        }

        await pendingStore.MarkSubmittingAsync(pending, cancellationToken);
        try
        {
            var receipts = await batchClient.SubmitAsync(groupId, pending, cancellationToken);
            var expectedIds = pending.Select(item => item.SourceMessageId).ToHashSet(StringComparer.Ordinal);
            var returnedIds = receipts.Select(item => item.SourceMessageId).ToHashSet(StringComparer.Ordinal);
            if (!expectedIds.SetEquals(returnedIds))
            {
                throw new InvalidOperationException("Batch response did not contain one receipt per submitted item.");
            }

            await pendingStore.ApplyReceiptsAsync(groupId, receipts, cancellationToken);
            return new LocalSyncSubmissionResult(
                pending.Length,
                receipts.Count(item => item.Status == "accepted"),
                receipts.Count(item => item.Status == "duplicate"),
                receipts.Count(item => item.Status == "invalid"));
        }
        catch
        {
            await pendingStore.RecoverSubmittingAsync(cancellationToken);
            throw;
        }
    }
}
