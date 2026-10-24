using GymFlow.AccessAgent.Offline;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Tests.Offline;

public class SqlitePhysicalPassageOfflineStoreTests
{
    [Fact]
    public async Task EnqueueAndMarkSyncedAsync_ShouldPersistPassage()
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

            var passage =
                new PendingPhysicalPassage(
                    Guid.NewGuid(),
                    now.AddSeconds(-2),
                    now,
                    null,
                    "door-a");

            await store.EnqueueAsync(
                passage,
                CancellationToken.None);

            Assert.Equal(
                1,
                await store.CountPendingAsync(
                    CancellationToken.None));

            var pending =
                await store.GetPendingAsync(
                    100,
                    CancellationToken.None);

            var result =
                Assert.Single(
                    pending);

            Assert.Equal(
                passage.RequestId,
                result.RequestId);

            Assert.Equal(
                passage.OccurredAt,
                result.OccurredAt);

            Assert.Equal(
                passage.CreatedAt,
                result.CreatedAt);

            Assert.Null(
                result.SyncedAt);

            await store.MarkSyncedAsync(
                passage.RequestId,
                now.AddMinutes(1),
                CancellationToken.None);

            Assert.Equal(
                0,
                await store.CountPendingAsync(
                    CancellationToken.None));

            Assert.Empty(
                await store.GetPendingAsync(
                    100,
                    CancellationToken.None));
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    [Fact]
    public async Task EnqueueAsync_WhenRequestAlreadyExists_ShouldBeIdempotent()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var store =
                CreateStore(
                    databasePath);

            var requestId =
                Guid.NewGuid();

            var now =
                DateTime.UtcNow;

            var first =
                new PendingPhysicalPassage(
                    requestId,
                    now,
                    now,
                    null,
                    "door-a");

            var duplicate =
                new PendingPhysicalPassage(
                    requestId,
                    now.AddMinutes(5),
                    now.AddMinutes(5),
                    null,
                    "door-b");

            await store.EnqueueAsync(
                first,
                CancellationToken.None);

            await store.EnqueueAsync(
                duplicate,
                CancellationToken.None);

            var pending =
                await store.GetPendingAsync(
                    100,
                    CancellationToken.None);

            var result =
                Assert.Single(
                    pending);

            Assert.Equal(
                first.OccurredAt,
                result.OccurredAt);

            Assert.Equal(
                first.CreatedAt,
                result.CreatedAt);
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    private static SqlitePhysicalPassageOfflineStore
        CreateStore(
            string databasePath)
    {
        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    DatabasePath =
                        databasePath
                });

        return new SqlitePhysicalPassageOfflineStore(
            options);
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"avelri-passage-{Guid.NewGuid():N}.db");
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
