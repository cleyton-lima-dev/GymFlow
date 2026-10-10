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

    private const string ConfigurationVersionKey =
        "PhysicalAccess.ConfigurationVersion";

    private readonly AccessOfflineStoreOptions _options;

    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public SqliteAccessReleaseControl(
        IOptions<AccessOfflineStoreOptions> options)
    {
        _options = options.Value;
    }

    public async Task<AccessReleaseConfiguration>
        GetConfigurationAsync(
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
            SELECT "Key", Value
            FROM AgentSettings
            WHERE "Key" IN
            (
                $releaseEnabledKey,
                $configurationVersionKey
            );
            """;

        command.Parameters.AddWithValue(
            "$releaseEnabledKey",
            ReleaseEnabledKey);

        command.Parameters.AddWithValue(
            "$configurationVersionKey",
            ConfigurationVersionKey);

        var releaseEnabled = false;
        long? configurationVersion = null;

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            var key =
                reader.GetString(0);

            var value =
                reader.GetString(1);

            if (string.Equals(
                    key,
                    ReleaseEnabledKey,
                    StringComparison.Ordinal))
            {
                releaseEnabled =
                    string.Equals(
                        value,
                        "1",
                        StringComparison.Ordinal);
            }
            else if (string.Equals(
                         key,
                         ConfigurationVersionKey,
                         StringComparison.Ordinal) &&
                     long.TryParse(
                         value,
                         NumberStyles.Integer,
                         CultureInfo.InvariantCulture,
                         out var parsedVersion) &&
                     parsedVersion >= 1)
            {
                configurationVersion =
                    parsedVersion;
            }
        }

        return new AccessReleaseConfiguration(
            releaseEnabled,
            configurationVersion);
    }

    public async Task<bool> IsReleaseEnabledAsync(
        CancellationToken cancellationToken)
    {
        var configuration =
            await GetConfigurationAsync(
                cancellationToken);

        return
            configuration.ReleaseEnabled &&
            configuration.ConfigurationVersion.HasValue;
    }

    public async Task ApplyConfigurationAsync(
        bool releaseEnabled,
        long configurationVersion,
        CancellationToken cancellationToken)
    {
        if (configurationVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configurationVersion),
                "A versão da configuração deve ser maior que zero.");
        }

        await InitializeAsync(
            cancellationToken);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        var updatedAt =
            DateTime.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

        await UpsertSettingAsync(
            connection,
            transaction,
            ReleaseEnabledKey,
            releaseEnabled ? "1" : "0",
            updatedAt,
            cancellationToken);

        await UpsertSettingAsync(
            connection,
            transaction,
            ConfigurationVersionKey,
            configurationVersion.ToString(
                CultureInfo.InvariantCulture),
            updatedAt,
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private static async Task UpsertSettingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        string updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction =
            transaction;

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
            key);

        command.Parameters.AddWithValue(
            "$value",
            value);

        command.Parameters.AddWithValue(
            "$updatedAt",
            updatedAt);

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
