using System.Globalization;
using GymFlow.AccessAgent.Offline;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Services;

public sealed class SqliteAccessReleaseControl :
    IAccessReleaseControl
{
    private const string ReleaseEnabledKey =
        "PhysicalAccess.ReleaseEnabled";

    private readonly AccessOfflineStoreOptions _options;

    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public SqliteAccessReleaseControl(
        IOptions<AccessOfflineStoreOptions> options)
    {
        _options = options.Value;
    }

    public async Task<bool> IsReleaseEnabledAsync(
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
            SELECT Value
            FROM AgentSettings
            WHERE "Key" = $key
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$key",
            ReleaseEnabledKey);

        var value =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return string.Equals(
            Convert.ToString(
                value,
                CultureInfo.InvariantCulture),
            "1",
            StringComparison.Ordinal);
    }

    public Task EnableAsync(
        CancellationToken cancellationToken)
    {
        return SetReleaseEnabledAsync(
            true,
            cancellationToken);
    }

    public Task DisableAsync(
        CancellationToken cancellationToken)
    {
        return SetReleaseEnabledAsync(
            false,
            cancellationToken);
    }

    private async Task SetReleaseEnabledAsync(
        bool enabled,
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
            INSERT INTO AgentSettings
            (
                "Key",
                Value,
                UpdatedAt
            )
            VALUES
            (
                $key,
                $value,
                $updatedAt
            )
            ON CONFLICT("Key")
            DO UPDATE SET
                Value = excluded.Value,
                UpdatedAt = excluded.UpdatedAt;
            """;

        command.Parameters.AddWithValue(
            "$key",
            ReleaseEnabledKey);

        command.Parameters.AddWithValue(
            "$value",
            enabled ? "1" : "0");

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private async Task InitializeAsync(
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

            if (!string.IsNullOrWhiteSpace(directory))
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

                CREATE TABLE IF NOT EXISTS AgentSettings
                (
                    "Key" TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
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

    private async Task<SqliteConnection>
        OpenConnectionAsync(
            CancellationToken cancellationToken)
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = _options.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

        var connection =
            new SqliteConnection(
                connectionString);

        await connection.OpenAsync(
            cancellationToken);

        return connection;
    }
}
