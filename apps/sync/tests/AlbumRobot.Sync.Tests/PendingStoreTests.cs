using AlbumRobot.Sync.Core;
using Xunit;

namespace AlbumRobot.Sync.Tests;

public sealed class PendingStoreTests
{
    [Fact]
    public async Task UpsertIsIdempotentAndAcceptedReceiptRemovesItem()
    {
        await using var store = new PendingStore(":memory:");
        await store.InitializeAsync();
        var candidate = Candidate("message-001");

        await store.UpsertAsync(candidate);
        await store.UpsertAsync(candidate);

        Assert.Single(await store.ListPendingAsync());
        await store.ApplyReceiptsAsync("group-001", [new SyncItemReceipt("message-001", "accepted")]);
        Assert.Empty(await store.ListPendingAsync());
    }

    [Fact]
    public async Task SubmittingItemsRecoverAfterRestart()
    {
        await using var store = new PendingStore(":memory:");
        await store.InitializeAsync();
        var candidate = Candidate("message-002");
        await store.UpsertAsync(candidate);
        await store.MarkSubmittingAsync([candidate]);
        await store.RecoverSubmittingAsync();

        Assert.Single(await store.ListPendingAsync());
    }

    private static ShareCandidate Candidate(string sourceMessageId) => new(
        "group-001",
        sourceMessageId,
        "member-001",
        "Example Member",
        DateTimeOffset.Parse("2026-01-02T12:34:56+08:00"),
        new NeteaseAlbum("123456", "Example Album", "Example Artist", null, NeteaseAlbumDetector.CanonicalUrl("123456")));
}
