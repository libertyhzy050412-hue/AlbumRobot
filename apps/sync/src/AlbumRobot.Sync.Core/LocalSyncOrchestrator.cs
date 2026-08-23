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

        var total = new LocalSyncSubmissionResult(0, 0, 0, 0);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pending = (await pendingStore.ListPendingForGroupAsync(
                groupId,
                cancellationToken: cancellationToken)).ToArray();
            if (pending.Length == 0) return total;

            var batch = await SubmitBatchAsync(groupId, pending, cancellationToken);
            total = new LocalSyncSubmissionResult(
                total.Submitted + batch.Submitted,
                total.Accepted + batch.Accepted,
                total.Duplicates + batch.Duplicates,
                total.Invalid + batch.Invalid);
        }
    }

    private async Task<LocalSyncSubmissionResult> SubmitBatchAsync(
        string groupId,
        ShareCandidate[] pending,
        CancellationToken cancellationToken)
    {
        await pendingStore.MarkSubmittingAsync(pending, cancellationToken);
        try
        {
            var receipts = await batchClient.SubmitAsync(groupId, pending, cancellationToken);
            var expectedIds = pending.Select(item => item.SourceMessageId).ToHashSet(StringComparer.Ordinal);
            var returnedIds = receipts.Select(item => item.SourceMessageId).ToHashSet(StringComparer.Ordinal);
            if (
                receipts.Count != pending.Length ||
                returnedIds.Count != pending.Length ||
                !expectedIds.SetEquals(returnedIds))
            {
                throw new InvalidOperationException("Batch response did not contain one receipt per submitted item.");
            }
            if (receipts.Any(item => item.Status is not ("accepted" or "duplicate" or "invalid")))
            {
                throw new InvalidOperationException("Batch response contained an unsupported receipt status.");
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
