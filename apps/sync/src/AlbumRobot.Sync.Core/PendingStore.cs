using Microsoft.Data.Sqlite;

namespace AlbumRobot.Sync.Core;

public enum PendingStatus
{
    Pending,
    Submitting,
    Invalid,
}

public sealed record SyncItemReceipt(string SourceMessageId, string Status, string? Reason = null);

public sealed class PendingStore : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    public PendingStore(string dataSource)
    {
        connection = new SqliteConnection($"Data Source={dataSource};Pooling=False");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS pending_items (
                group_id TEXT NOT NULL,
                source_message_id TEXT NOT NULL,
                member_id TEXT NOT NULL,
                member_nickname TEXT NOT NULL,
                shared_at TEXT NOT NULL,
                album_id TEXT NOT NULL,
                title TEXT NOT NULL,
                artist TEXT NOT NULL,
                cover_url TEXT,
                netease_url TEXT,
                origin TEXT NOT NULL,
                state TEXT NOT NULL CHECK (state IN ('pending', 'submitting', 'invalid')),
                last_error TEXT,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (group_id, source_message_id)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertAsync(ShareCandidate candidate, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pending_items
              (group_id, source_message_id, member_id, member_nickname, shared_at,
               album_id, title, artist, cover_url, netease_url, origin, state, last_error, updated_at)
            VALUES ($group_id, $source_message_id, $member_id, $member_nickname, $shared_at,
                    $album_id, $title, $artist, $cover_url, $netease_url, $origin, 'pending', NULL, $updated_at)
            ON CONFLICT(group_id, source_message_id) DO UPDATE SET
              member_id = excluded.member_id,
              member_nickname = excluded.member_nickname,
              shared_at = excluded.shared_at,
              album_id = excluded.album_id,
              title = excluded.title,
              artist = excluded.artist,
              cover_url = excluded.cover_url,
              netease_url = excluded.netease_url,
              origin = excluded.origin,
              state = 'pending',
              last_error = NULL,
              updated_at = excluded.updated_at;
            """;
        AddCandidateParameters(command, candidate);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShareCandidate>> ListPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await ListPendingCoreAsync(groupId: null, limit, cancellationToken);
    }

    public async Task<IReadOnlyList<ShareCandidate>> ListPendingForGroupAsync(
        string groupId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        return await ListPendingCoreAsync(groupId, limit, cancellationToken);
    }

    public async Task<int> CountPendingAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pending_items WHERE state = 'pending' AND group_id = $group_id;";
        command.Parameters.AddWithValue("$group_id", groupId);
        return checked(Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)));
    }

    private async Task<IReadOnlyList<ShareCandidate>> ListPendingCoreAsync(
        string? groupId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT group_id, source_message_id, member_id, member_nickname, shared_at,
                   album_id, title, artist, cover_url, netease_url, origin
              FROM pending_items
             WHERE state = 'pending'
               AND ($group_id IS NULL OR group_id = $group_id)
             ORDER BY updated_at, source_message_id
             LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$group_id", (object?)groupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ShareCandidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ShareCandidate(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
                new NeteaseAlbum(
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? NeteaseAlbumDetector.CanonicalUrl(reader.GetString(5)) : reader.GetString(9)),
                reader.GetString(10)));
        }

        return items;
    }

    public async Task MarkSubmittingAsync(IEnumerable<ShareCandidate> items, CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in items)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                UPDATE pending_items
                   SET state = 'submitting', updated_at = $updated_at
                 WHERE group_id = $group_id AND source_message_id = $source_message_id AND state = 'pending';
                """;
            command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$group_id", item.GroupId);
            command.Parameters.AddWithValue("$source_message_id", item.SourceMessageId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecoverSubmittingAsync(CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE pending_items SET state = 'pending', updated_at = $updated_at WHERE state = 'submitting';";
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ApplyReceiptsAsync(string groupId, IEnumerable<SyncItemReceipt> receipts, CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var receipt in receipts)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.Parameters.AddWithValue("$group_id", groupId);
            command.Parameters.AddWithValue("$source_message_id", receipt.SourceMessageId);
            command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            if (receipt.Status is "accepted" or "duplicate")
            {
                command.CommandText = "DELETE FROM pending_items WHERE group_id = $group_id AND source_message_id = $source_message_id;";
            }
            else if (receipt.Status == "invalid")
            {
                command.CommandText = "UPDATE pending_items SET state = 'invalid', last_error = $reason, updated_at = $updated_at WHERE group_id = $group_id AND source_message_id = $source_message_id;";
                command.Parameters.AddWithValue("$reason", receipt.Reason ?? "invalid candidate");
            }
            else
            {
                command.CommandText = "UPDATE pending_items SET state = 'pending', last_error = $reason, updated_at = $updated_at WHERE group_id = $group_id AND source_message_id = $source_message_id;";
                command.Parameters.AddWithValue("$reason", receipt.Reason ?? "unknown batch result");
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static void AddCandidateParameters(SqliteCommand command, ShareCandidate candidate)
    {
        command.Parameters.AddWithValue("$group_id", candidate.GroupId);
        command.Parameters.AddWithValue("$source_message_id", candidate.SourceMessageId);
        command.Parameters.AddWithValue("$member_id", candidate.MemberId);
        command.Parameters.AddWithValue("$member_nickname", candidate.MemberNickname);
        command.Parameters.AddWithValue("$shared_at", candidate.SharedAt.ToString("O"));
        command.Parameters.AddWithValue("$album_id", candidate.Album.AlbumId);
        command.Parameters.AddWithValue("$title", candidate.Album.Title ?? "Untitled");
        command.Parameters.AddWithValue("$artist", candidate.Album.Artist ?? "Unknown artist");
        command.Parameters.AddWithValue("$cover_url", (object?)candidate.Album.CoverUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$netease_url", candidate.Album.CanonicalUrl);
        command.Parameters.AddWithValue("$origin", candidate.Origin);
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
    }

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
