using System.Globalization;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Offline;

public sealed class SqliteAccessOfflineStore :
    IAccessOfflineStore
{
    private readonly AccessOfflineStoreOptions _options;
    private readonly ILocalSensitiveDataProtector _protector;

    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public SqliteAccessOfflineStore(
        IOptions<AccessOfflineStoreOptions> options,
        ILocalSensitiveDataProtector protector)
    {
        _options = options.Value;
        _protector = protector;
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

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
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

                CREATE TABLE IF NOT EXISTS CachedPermissions
                (
                    ProviderKey TEXT NOT NULL,
                    CredentialType INTEGER NOT NULL,
                    LookupHash TEXT NOT NULL,
                    EncryptedExternalIdentifier TEXT NOT NULL,
                    Allowed INTEGER NOT NULL,
                    Reason TEXT NOT NULL,
                    ValidUntil TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    GymId TEXT NOT NULL DEFAULT '',
                    AgentId TEXT NOT NULL DEFAULT '',
                    DeviceKey TEXT NOT NULL DEFAULT '',
                    PRIMARY KEY
                    (
                        ProviderKey,
                        CredentialType,
                        LookupHash
                    )
                );

                CREATE TABLE IF NOT EXISTS PendingAccessEvents
                (
                    RequestId TEXT NOT NULL PRIMARY KEY,
                    ProviderKey TEXT NOT NULL,
                    CredentialType INTEGER NOT NULL,
                    LookupHash TEXT NOT NULL,
                    EncryptedExternalIdentifier TEXT NOT NULL,
                    OccurredAt TEXT NOT NULL,
                    Allowed INTEGER NOT NULL,
                     Reason TEXT NOT NULL,
                    Source INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    SyncedAt TEXT NULL,
                    DeviceKey TEXT NOT NULL DEFAULT '',
                    AgentId TEXT NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS
                    IX_PendingAccessEvents_SyncedAt_CreatedAt
                ON PendingAccessEvents
                (
                    SyncedAt,
                    CreatedAt
                );
                """;

            await command.ExecuteNonQueryAsync(
                cancellationToken);

            await EnsureDeviceKeyColumnAsync(connection, cancellationToken);
            await EnsureAgentIdColumnAsync(connection, "PendingAccessEvents", cancellationToken);
            await EnsureCacheScopeColumnsAsync(connection, cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<CachedAccessPermission?>
        GetPermissionAsync(
            string providerKey,
            AccessCredentialType credentialType,
            string externalIdentifier,
            DateTime utcNow,
            CancellationToken cancellationToken,
            Guid gymId = default,
            Guid agentId = default,
            string deviceKey = "")
    {
        await InitializeAsync(
            cancellationToken);

        var lookupHash = ComputeScopedLookupHash(
            gymId, agentId, deviceKey, providerKey,
            credentialType, externalIdentifier);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                EncryptedExternalIdentifier,
                Allowed,
                Reason,
                ValidUntil,
                UpdatedAt,
                GymId,
                AgentId,
                DeviceKey
            FROM CachedPermissions
            WHERE ProviderKey = $providerKey
              AND CredentialType = $credentialType
              AND LookupHash = $lookupHash
              AND GymId = $gymId
              AND AgentId = $agentId
              AND DeviceKey = $deviceKey
              AND ValidUntil > $utcNow
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$providerKey",
            providerKey);

        command.Parameters.AddWithValue(
            "$credentialType",
            (int)credentialType);

        command.Parameters.AddWithValue(
            "$lookupHash",
            lookupHash);

        command.Parameters.AddWithValue("$gymId", gymId.ToString());
        command.Parameters.AddWithValue("$agentId", agentId.ToString());
        command.Parameters.AddWithValue("$deviceKey", deviceKey);

        command.Parameters.AddWithValue(
            "$utcNow",
            ToDatabaseDateTime(utcNow));

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        DateTime updatedAt = default;
        if (credentialType == AccessCredentialType.PinExternalId &&
            (reader.IsDBNull(4) ||
             !DateTime.TryParseExact(reader.GetString(4), "O",
                 CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                 out updatedAt)))
            return null;

        if (credentialType != AccessCredentialType.PinExternalId)
            updatedAt = ParseDatabaseDateTime(reader.GetString(4));

        if (credentialType == AccessCredentialType.PinExternalId &&
            (updatedAt == default || updatedAt > utcNow ||
             updatedAt.AddMinutes(5) <= utcNow))
            return null;

        return new CachedAccessPermission(
            providerKey,
            credentialType,
            _protector.Unprotect(
                reader.GetString(0)),
            reader.GetInt32(1) != 0,
            reader.GetString(2),
            ParseDatabaseDateTime(
                reader.GetString(3)),
            updatedAt,
            Guid.Parse(reader.GetString(5)),
            Guid.Parse(reader.GetString(6)),
            reader.GetString(7));
    }

    public async Task UpsertPermissionAsync(
        CachedAccessPermission permission,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        var lookupHash = ComputeScopedLookupHash(
            permission.GymId, permission.AgentId, permission.DeviceKey,
            permission.ProviderKey, permission.CredentialType,
            permission.ExternalIdentifier);

        var protectedIdentifier =
            _protector.Protect(
                permission.ExternalIdentifier);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO CachedPermissions
            (
                ProviderKey,
                CredentialType,
                LookupHash,
                EncryptedExternalIdentifier,
                Allowed,
                Reason,
                ValidUntil,
                UpdatedAt,
                GymId,
                AgentId,
                DeviceKey
            )
            VALUES
            (
                $providerKey,
                $credentialType,
                $lookupHash,
                $encryptedExternalIdentifier,
                $allowed,
                $reason,
                $validUntil,
                $updatedAt,
                $gymId,
                $agentId,
                $deviceKey
            )
            ON CONFLICT
            (
                ProviderKey,
                CredentialType,
                LookupHash
            )
            DO UPDATE SET
                EncryptedExternalIdentifier =
                    excluded.EncryptedExternalIdentifier,
                Allowed =
                    excluded.Allowed,
                Reason =
                    excluded.Reason,
                ValidUntil =
                    excluded.ValidUntil,
                UpdatedAt =
                    excluded.UpdatedAt,
                GymId = excluded.GymId,
                AgentId = excluded.AgentId,
                DeviceKey = excluded.DeviceKey;
            """;

        command.Parameters.AddWithValue(
            "$providerKey",
            permission.ProviderKey);

        command.Parameters.AddWithValue(
            "$credentialType",
            (int)permission.CredentialType);

        command.Parameters.AddWithValue(
            "$lookupHash",
            lookupHash);

        command.Parameters.AddWithValue(
            "$encryptedExternalIdentifier",
            protectedIdentifier);

        command.Parameters.AddWithValue(
            "$allowed",
            permission.Allowed ? 1 : 0);

        command.Parameters.AddWithValue(
            "$reason",
            permission.Reason);

        command.Parameters.AddWithValue(
            "$validUntil",
            ToDatabaseDateTime(
                permission.ValidUntil));

        command.Parameters.AddWithValue(
            "$updatedAt",
            ToDatabaseDateTime(
                permission.UpdatedAt));

        command.Parameters.AddWithValue("$gymId", permission.GymId.ToString());
        command.Parameters.AddWithValue("$agentId", permission.AgentId.ToString());
        command.Parameters.AddWithValue("$deviceKey", permission.DeviceKey);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task EnqueueEventAsync(
        PendingAccessEvent accessEvent,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        var lookupHash =
            _protector.ComputeLookupHash(
                accessEvent.ProviderKey,
                (int)accessEvent.CredentialType,
                accessEvent.ExternalIdentifier);

        var protectedIdentifier =
            _protector.Protect(
                accessEvent.ExternalIdentifier);

        await using var connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT OR IGNORE INTO PendingAccessEvents
            (
                RequestId,
                ProviderKey,
                CredentialType,
                LookupHash,
                EncryptedExternalIdentifier,
                OccurredAt,
                Allowed,
            Reason,
            Source,
            CreatedAt,
                SyncedAt,
                DeviceKey,
                AgentId
            )
            VALUES
            (
                $requestId,
                $providerKey,
                $credentialType,
                $lookupHash,
                $encryptedExternalIdentifier,
                $occurredAt,
                $allowed,
            $reason,
            $source,
            $createdAt,
                $syncedAt,
            $deviceKey,
            $agentId
            );
            """;

        command.Parameters.AddWithValue(
            "$requestId",
            accessEvent.RequestId.ToString());

        command.Parameters.AddWithValue(
            "$providerKey",
            accessEvent.ProviderKey);

        command.Parameters.AddWithValue(
            "$credentialType",
            (int)accessEvent.CredentialType);

        command.Parameters.AddWithValue(
            "$lookupHash",
            lookupHash);

        command.Parameters.AddWithValue(
            "$encryptedExternalIdentifier",
            protectedIdentifier);

        command.Parameters.AddWithValue(
            "$occurredAt",
            ToDatabaseDateTime(
                accessEvent.OccurredAt));

        command.Parameters.AddWithValue(
            "$allowed",
            accessEvent.Allowed ? 1 : 0);

        command.Parameters.AddWithValue(
            "$reason",
            accessEvent.Reason);

        command.Parameters.AddWithValue(
             "$source",
                (int)accessEvent.Source);

        command.Parameters.AddWithValue(
            "$createdAt",
            ToDatabaseDateTime(
                accessEvent.CreatedAt));

        command.Parameters.AddWithValue(
            "$syncedAt",
            accessEvent.SyncedAt is null
                ? DBNull.Value
                : ToDatabaseDateTime(
                    accessEvent.SyncedAt.Value));

        command.Parameters.AddWithValue(
            "$deviceKey",
            accessEvent.DeviceKey);
        command.Parameters.AddWithValue(
            "$agentId",
            accessEvent.AgentId?.ToString() ?? "");

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingAccessEvent>>
        GetPendingEventsAsync(
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
                ProviderKey,
                CredentialType,
                EncryptedExternalIdentifier,
                OccurredAt,
               Allowed,
            Reason,
            Source,
            CreatedAt,
                SyncedAt,
                DeviceKey,
                AgentId
            FROM PendingAccessEvents
            WHERE SyncedAt IS NULL
              AND length(trim(DeviceKey)) > 0
              AND length(trim(AgentId)) > 0
            ORDER BY CreatedAt
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        var events =
            new List<PendingAccessEvent>();

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            events.Add(
    new PendingAccessEvent(
        Guid.Parse(
            reader.GetString(0)),
        reader.GetString(1),
        (AccessCredentialType)
            reader.GetInt32(2),
        _protector.Unprotect(
            reader.GetString(3)),
        ParseDatabaseDateTime(
            reader.GetString(4)),
        reader.GetInt32(5) != 0,
        reader.GetString(6),
        (AccessDecisionSource)
            reader.GetInt32(7),
        ParseDatabaseDateTime(
            reader.GetString(8)),
        reader.IsDBNull(9)
            ? null
            : ParseDatabaseDateTime(
                reader.GetString(9)),
        reader.GetString(10),
        Guid.TryParse(reader.GetString(11), out var agentId)
            ? agentId
            : null));
        }

        return events;
    }

    public async Task<int> CountPendingEventsAsync(
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
            FROM PendingAccessEvents
            WHERE SyncedAt IS NULL;
            """;

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt32(
            result,
            CultureInfo.InvariantCulture);
    }
    public async Task MarkEventSyncedAsync(
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
            UPDATE PendingAccessEvents
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
                DataSource = _options.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

        var connection =
            new SqliteConnection(
                builder.ToString());

        await connection.OpenAsync(
            cancellationToken);

        return connection;
    }

    private static async Task EnsureDeviceKeyColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(PendingAccessEvents);";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var found = false;
        while (await reader.ReadAsync(cancellationToken))
            found |= string.Equals(reader.GetString(1), "DeviceKey", StringComparison.OrdinalIgnoreCase);
        await reader.DisposeAsync();

        if (found)
            return;

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE PendingAccessEvents ADD COLUMN DeviceKey TEXT NOT NULL DEFAULT '';";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureAgentIdColumnAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var found = false;
        while (await reader.ReadAsync(cancellationToken))
            found |= string.Equals(reader.GetString(1), "AgentId", StringComparison.OrdinalIgnoreCase);
        await reader.DisposeAsync();
        if (found) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN AgentId TEXT NOT NULL DEFAULT '';";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCacheScopeColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        foreach (var column in new[] { "GymId", "AgentId", "DeviceKey" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(CachedPermissions);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var found = false;
            while (await reader.ReadAsync(cancellationToken))
                found |= string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase);
            await reader.DisposeAsync();
            if (found) continue;
            await using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE CachedPermissions ADD COLUMN {column} TEXT NOT NULL DEFAULT '';";
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }
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

    private string ComputeScopedLookupHash(
        Guid gymId,
        Guid agentId,
        string deviceKey,
        string providerKey,
        AccessCredentialType credentialType,
        string externalIdentifier) =>
        _protector.ComputeLookupHash(
            $"{gymId:N}|{agentId:N}|{deviceKey}|{providerKey}",
            (int)credentialType,
            externalIdentifier);

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
