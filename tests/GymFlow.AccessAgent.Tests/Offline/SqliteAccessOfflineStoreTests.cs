using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Offline;

public class SqliteAccessOfflineStoreTests
{
    [Theory]
    [InlineData(AccessCredentialType.PinExternalId, 6, false)]
    [InlineData(AccessCredentialType.PinExternalId, 4, true)]
    [InlineData(AccessCredentialType.PinExternalId, -1, false)]
    [InlineData(AccessCredentialType.Card, 6, true)]
    [InlineData(AccessCredentialType.BiometricExternalId, 6, true)]
    public async Task RestartedStoreCapsLegacyPinWithoutChangingOtherCredentials(
        AccessCredentialType type, int ageMinutes, bool expectedAvailable)
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var now = DateTime.UtcNow;
            var permission = new CachedAccessPermission("toletus-litenet2",
                type, "0007", true, "Eligible", now.AddMinutes(20),
                ageMinutes < 0 ? default : now.AddMinutes(-ageMinutes));
            await CreateStore(databasePath).UpsertPermissionAsync(permission,
                CancellationToken.None);

            var restarted = CreateStore(databasePath);
            var result = await restarted.GetPermissionAsync(permission.ProviderKey,
                type, permission.ExternalIdentifier, now, CancellationToken.None);

            Assert.Equal(expectedAvailable, result is not null);
        }
        finally
        {
            Cleanup(databasePath);
        }
    }

    [Fact]
    public async Task RestartedStoreRejectsPinWithoutParseableWriteTimestamp()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var now = DateTime.UtcNow;
            await CreateStore(databasePath).UpsertPermissionAsync(
                new CachedAccessPermission("toletus-litenet2",
                    AccessCredentialType.PinExternalId, "0007", true, "Eligible",
                    now.AddMinutes(20), now), CancellationToken.None);
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE CachedPermissions SET UpdatedAt = '' WHERE CredentialType = 5";
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }

            Assert.Null(await CreateStore(databasePath).GetPermissionAsync(
                "toletus-litenet2", AccessCredentialType.PinExternalId,
                "0007", now, CancellationToken.None));
        }
        finally
        {
            Cleanup(databasePath);
        }
    }

    [Fact]
    public async Task UpsertAndGetPermissionAsync_ReturnsCachedPermission()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var store =
                CreateStore(
                    databasePath);

            var now =
                DateTime.UtcNow;

            var permission =
                new CachedAccessPermission(
                    "toletus-litenet2",
                    AccessCredentialType.Card,
                    "12345",
                    true,
                    "Eligible",
                    now.AddMinutes(30),
                    now);

            await store.UpsertPermissionAsync(
                permission,
                CancellationToken.None);

            var result =
                await store.GetPermissionAsync(
                    permission.ProviderKey,
                    permission.CredentialType,
                    permission.ExternalIdentifier,
                    now,
                    CancellationToken.None);

            Assert.NotNull(result);

            Assert.Equal(
                permission.ProviderKey,
                result.ProviderKey);

            Assert.Equal(
                permission.CredentialType,
                result.CredentialType);

            Assert.Equal(
                permission.ExternalIdentifier,
                result.ExternalIdentifier);

            Assert.True(
                result.Allowed);

            Assert.Equal(
                "Eligible",
                result.Reason);

            Assert.Equal(
                permission.ValidUntil,
                result.ValidUntil);
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    [Fact]
    public async Task CachedPermission_IsScopedToGymAgentAndDevice()
    {
        var path = CreateDatabasePath();
        try
        {
            var store = CreateStore(path);
            var now = DateTime.UtcNow;
            var gymId = Guid.NewGuid();
            var agentId = Guid.NewGuid();
            var permission = new CachedAccessPermission("toletus-litenet2",
                AccessCredentialType.Card, "scoped-card", true, "Eligible",
                now.AddMinutes(20), now, gymId, agentId, "door-a");
            await store.UpsertPermissionAsync(permission, CancellationToken.None);

            Assert.NotNull(await store.GetPermissionAsync(permission.ProviderKey,
                permission.CredentialType, permission.ExternalIdentifier, now,
                CancellationToken.None, gymId, agentId, "door-a"));
            Assert.Null(await store.GetPermissionAsync(permission.ProviderKey,
                permission.CredentialType, permission.ExternalIdentifier, now,
                CancellationToken.None, Guid.NewGuid(), agentId, "door-a"));
            Assert.Null(await store.GetPermissionAsync(permission.ProviderKey,
                permission.CredentialType, permission.ExternalIdentifier, now,
                CancellationToken.None, gymId, Guid.NewGuid(), "door-a"));
            Assert.Null(await store.GetPermissionAsync(permission.ProviderKey,
                permission.CredentialType, permission.ExternalIdentifier, now,
                CancellationToken.None, gymId, agentId, "door-b"));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task LegacyPendingQueue_IsPreservedButNotIncludedInSyncBatch()
    {
        var path = CreateDatabasePath();
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE PendingAccessEvents (
                        RequestId TEXT NOT NULL PRIMARY KEY, ProviderKey TEXT NOT NULL,
                        CredentialType INTEGER NOT NULL, LookupHash TEXT NOT NULL,
                        EncryptedExternalIdentifier TEXT NOT NULL, OccurredAt TEXT NOT NULL,
                        Allowed INTEGER NOT NULL, Reason TEXT NOT NULL, Source INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL, SyncedAt TEXT NULL);
                    INSERT INTO PendingAccessEvents VALUES
                        ('legacy-request','toletus-litenet2',1,'hash','protected:12345',
                         '2026-10-24T10:00:00.0000000Z',1,'Eligible',2,
                         '2026-10-24T10:00:00.0000000Z',NULL);
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var store = CreateStore(path);
            Assert.Equal(1, await store.CountPendingEventsAsync(CancellationToken.None));
            Assert.Empty(await store.GetPendingEventsAsync(100, CancellationToken.None));
            await using var verify = new SqliteConnection($"Data Source={path}");
            await verify.OpenAsync();
            await using var count = verify.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM PendingAccessEvents WHERE RequestId='legacy-request';";
            Assert.Equal(1L, await count.ExecuteScalarAsync());
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task EnqueueAndMarkSyncedAsync_HandlesPendingEventAndCount()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var store =
                CreateStore(
                    databasePath);

            var now =
                DateTime.UtcNow;

            var accessEvent =
                new PendingAccessEvent(
                    Guid.NewGuid(),
                    "toletus-litenet2",
                    AccessCredentialType.Card,
                    "12345",
                    now,
                    true,
                    "Eligible",
                    AccessDecisionSource.OfflineCache,
                    now,
                    null,
                    "door-a",
                    Guid.NewGuid());

            await store.EnqueueEventAsync(
                accessEvent,
                CancellationToken.None);

            Assert.Equal(
                1,
                await store.CountPendingEventsAsync(
                    CancellationToken.None));

            var restarted = CreateStore(databasePath);
            var pending =
                await restarted.GetPendingEventsAsync(
                    100,
                    CancellationToken.None);

            var result =
                Assert.Single(
                    pending);

            Assert.Equal(
                accessEvent.RequestId,
                result.RequestId);

            Assert.Equal(
                accessEvent.ExternalIdentifier,
                result.ExternalIdentifier);
            Assert.Equal(accessEvent.OccurredAt, result.OccurredAt);
            Assert.Equal(accessEvent.CreatedAt, result.CreatedAt);
            Assert.Equal(accessEvent.Allowed, result.Allowed);
            Assert.Equal(accessEvent.Reason, result.Reason);
            Assert.Equal(accessEvent.Source, result.Source);
            Assert.Equal(accessEvent.AgentId, result.AgentId);
            Assert.Equal(accessEvent.DeviceKey, result.DeviceKey);

            Assert.Null(
                result.SyncedAt);

            var syncedAt =
                now.AddMinutes(1);

            await restarted.MarkEventSyncedAsync(
                accessEvent.RequestId,
                syncedAt,
                CancellationToken.None);

            Assert.Equal(
                0,
                await store.CountPendingEventsAsync(
                    CancellationToken.None));

            var remaining =
                await store.GetPendingEventsAsync(
                    100,
                    CancellationToken.None);

            Assert.Empty(
                remaining);
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    [Fact]
    public async Task GetPermissionAsync_WhenPermissionIsExpired_ReturnsNull()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var store =
                CreateStore(
                    databasePath);

            var now =
                DateTime.UtcNow;

            var permission =
                new CachedAccessPermission(
                    "toletus-litenet2",
                    AccessCredentialType.Card,
                    "12345",
                    true,
                    "Eligible",
                    now.AddMinutes(-1),
                    now.AddMinutes(-30));

            await store.UpsertPermissionAsync(
                permission,
                CancellationToken.None);

            var result =
                await store.GetPermissionAsync(
                    permission.ProviderKey,
                    permission.CredentialType,
                    permission.ExternalIdentifier,
                    now,
                    CancellationToken.None);

            Assert.Null(
                result);
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    private static SqliteAccessOfflineStore
        CreateStore(
            string databasePath)
    {
        var protector =
            Substitute.For<
                ILocalSensitiveDataProtector>();

        protector
            .ComputeLookupHash(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>())
            .Returns(
                call =>
                    $"{call.ArgAt<string>(0)}|" +
                    $"{call.ArgAt<int>(1)}|" +
                    $"{call.ArgAt<string>(2)}");

        protector
            .Protect(
                Arg.Any<string>())
            .Returns(
                call =>
                    $"protected:{call.Arg<string>()}");

        protector
            .Unprotect(
                Arg.Any<string>())
            .Returns(
                call =>
                    call.Arg<string>()
                        .Replace(
                            "protected:",
                            string.Empty));

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    DatabasePath =
                        databasePath
                });

        return new SqliteAccessOfflineStore(
            options,
            protector);
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"avelri-access-agent-{Guid.NewGuid():N}.db");
    }

    private static void Cleanup(
        string databasePath)
    {
        SqliteConnection.ClearAllPools();

        DeleteIfExists(
            databasePath);

        DeleteIfExists(
            $"{databasePath}-wal");

        DeleteIfExists(
            $"{databasePath}-shm");
    }

    private static void DeleteIfExists(
        string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
