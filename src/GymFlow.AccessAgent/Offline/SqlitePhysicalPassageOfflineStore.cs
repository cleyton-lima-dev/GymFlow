using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Offline;

public sealed class SqlitePhysicalPassageOfflineStore :
    IPhysicalPassageOfflineStore
{
    private readonly AccessOfflineStoreOptions
        _options;

    private readonly SemaphoreSlim
        _initializationLock =
            new(1, 1);

    private bool _initialized;

    public SqlitePhysicalPassageOfflineStore(
        IOptions<AccessOfflineStoreOptions> options)
    {
        _options = options.Value;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _initializationLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_initialized)
                return;

            var directory =
                Path.GetDirectoryName(
                    _options.DatabasePath);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            await using var connection =
                await OpenConnectionAsync(
                    cancellationToken);

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
                PRAGMA journal_mode = WAL;
                PRAGMA busy_timeout = 5000;

                CREATE TABLE IF NOT EXISTS PendingPhysicalPassages
                (
                    RequestId TEXT NOT NULL PRIMARY KEY,
                    OccurredAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    SyncedAt TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS
                    IX_PendingPhysicalPassages_SyncedAt_CreatedAt
                ON PendingPhysicalPassages
                (
                    SyncedAt,
                    CreatedAt
                );
                """;

            await command.ExecuteNonQueryAsync(
                cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task EnqueueAsync(
        PendingPhysicalPassage passage,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT OR IGNORE INTO PendingPhysicalPassages
            (
                RequestId,
                OccurredAt,
                CreatedAt,
                SyncedAt
            )
            VALUES
            (
                $requestId,
                $occurredAt,
                $createdAt,
                $syncedAt
            );
            """;

        command.Parameters.AddWithValue(
            "$requestId",
            passage.RequestId.ToString());

        command.Parameters.AddWithValue(
            "$occurredAt",
            ToDatabaseDateTime(
                passage.OccurredAt));

        command.Parameters.AddWithValue(
            "$createdAt",
            ToDatabaseDateTime(
                passage.CreatedAt));

        command.Parameters.AddWithValue(
            "$syncedAt",
            passage.SyncedAt is null
                ? DBNull.Value
                : ToDatabaseDateTime(
                    passage.SyncedAt.Value));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingPhysicalPassage>>
        GetPendingAsync(
            int limit,
            CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                RequestId,
                OccurredAt,
                CreatedAt,
                SyncedAt
            FROM PendingPhysicalPassages
            WHERE SyncedAt IS NULL
            ORDER BY CreatedAt
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        var passages =
            new List<PendingPhysicalPassage>();

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            passages.Add(
                new PendingPhysicalPassage(
                    Guid.Parse(
                        reader.GetString(0)),
                    ParseDatabaseDateTime(
                        reader.GetString(1)),
                    ParseDatabaseDateTime(
                        reader.GetString(2)),
                    reader.IsDBNull(3)
                        ? null
                        : ParseDatabaseDateTime(
                            reader.GetString(3))));
        }

        return passages;
    }

    public async Task<int> CountPendingAsync(
        CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM PendingPhysicalPassages
            WHERE SyncedAt IS NULL;
            """;

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt32(
            result,
            CultureInfo.InvariantCulture);
    }

    public async Task MarkSyncedAsync(
        Guid requestId,
        DateTime syncedAt,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE PendingPhysicalPassages
            SET SyncedAt = $syncedAt
            WHERE RequestId = $requestId;
            """;

        command.Parameters.AddWithValue(
            "$requestId",
            requestId.ToString());

        command.Parameters.AddWithValue(
            "$syncedAt",
            ToDatabaseDateTime(
                syncedAt));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private async Task<SqliteConnection>
        OpenConnectionAsync(
            CancellationToken cancellationToken)
    {
        var builder =
            new SqliteConnectionStringBuilder
            {
                DataSource =
                    _options.DatabasePath,
                Mode =
                    SqliteOpenMode.ReadWriteCreate,
                Cache =
                    SqliteCacheMode.Shared
            };

        var connection =
            new SqliteConnection(
                builder.ToString());

        await connection.OpenAsync(
            cancellationToken);

        return connection;
    }

    private static string ToDatabaseDateTime(
        DateTime value)
    {
        return value
            .ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDatabaseDateTime(
        string value)
    {
        return DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
            .ToUniversalTime();
    }
}