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
                     SyncedAt TEXT NULL
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
            CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        var lookupHash =
            _protector.ComputeLookupHash(
                providerKey,
                (int)credentialType,
                externalIdentifier);

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
                UpdatedAt
            FROM CachedPermissions
            WHERE ProviderKey = $providerKey
              AND CredentialType = $credentialType
              AND LookupHash = $lookupHash
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

        return new CachedAccessPermission(
            providerKey,
            credentialType,
            _protector.Unprotect(
                reader.GetString(0)),
            reader.GetInt32(1) != 0,
            reader.GetString(2),
            ParseDatabaseDateTime(
                reader.GetString(3)),
            ParseDatabaseDateTime(
                reader.GetString(4)));
    }

    public async Task UpsertPermissionAsync(
        CachedAccessPermission permission,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(
            cancellationToken);

        var lookupHash =
            _protector.ComputeLookupHash(
                permission.ProviderKey,
                (int)permission.CredentialType,
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
                UpdatedAt
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
                $updatedAt
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
                    excluded.UpdatedAt;
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
            SyncedAt
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
            $syncedAt
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
            SyncedAt
            FROM PendingAccessEvents
            WHERE SyncedAt IS NULL
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
                reader.GetString(9))));
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
