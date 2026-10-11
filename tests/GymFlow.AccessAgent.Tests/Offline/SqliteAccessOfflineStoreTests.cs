using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Offline;

public class SqliteAccessOfflineStoreTests
{
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
                    null);

            await store.EnqueueEventAsync(
                accessEvent,
                CancellationToken.None);

            Assert.Equal(
                1,
                await store.CountPendingEventsAsync(
                    CancellationToken.None));

            var pending =
                await store.GetPendingEventsAsync(
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

            Assert.Null(
                result.SyncedAt);

            var syncedAt =
                now.AddMinutes(1);

            await store.MarkEventSyncedAsync(
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
