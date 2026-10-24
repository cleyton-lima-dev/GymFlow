using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Adapters.Toletus;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Tests.Offline;

public sealed class PhysicalPassageConfirmationCoordinatorTests
{
    [Fact]
    public async Task SyntheticPassage_IsQueuedOnlyOnceForItsAllowedDeviceAndRequest()
    {
        await using var database = await TestDatabase.CreateAsync();
        var coordinator = new PhysicalPassageConfirmationCoordinator(
            database.Store,
            TimeProvider.System,
            enabled: true);
        var board = new object();
        var requestId = Guid.NewGuid();
        var authorizedAt = DateTime.UtcNow.AddSeconds(-1);

        Assert.True(coordinator.TryArm(board, "front-door", requestId, authorizedAt));
        Assert.False(coordinator.TryArm(board, "front-door", Guid.NewGuid(), authorizedAt));

        var occurrence = DateTime.UtcNow;
        Assert.True(await coordinator.ConfirmAsync(
            board, "front-door", occurrence, CancellationToken.None));
        Assert.False(await coordinator.ConfirmAsync(
            board, "front-door", occurrence, CancellationToken.None));

        var passage = Assert.Single(await database.Store.GetPendingAsync(
            10, CancellationToken.None));
        Assert.Equal(requestId, passage.RequestId);
        Assert.Equal(occurrence, passage.OccurredAt);
    }

    [Fact]
    public async Task SyntheticPassage_CannotCrossDeviceKeyOrBoardInstance()
    {
        await using var database = await TestDatabase.CreateAsync();
        var coordinator = new PhysicalPassageConfirmationCoordinator(
            database.Store,
            TimeProvider.System,
            enabled: true);
        var board = new object();
        var requestId = Guid.NewGuid();

        Assert.True(coordinator.TryArm(
            board, "front-door", requestId, DateTime.UtcNow.AddSeconds(-1)));
        Assert.False(await coordinator.ConfirmAsync(
            new object(), "front-door", DateTime.UtcNow, CancellationToken.None));
        Assert.False(await coordinator.ConfirmAsync(
            board, "back-door", DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(0, await database.Store.CountPendingAsync(CancellationToken.None));

        Assert.True(await coordinator.ConfirmAsync(
            board, "front-door", DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(requestId, Assert.Single(await database.Store.GetPendingAsync(
            10, CancellationToken.None)).RequestId);
    }

    [Fact]
    public async Task SyntheticPassage_WithoutAllowedAuthorizationCannotCreateCheckInWork()
    {
        await using var database = await TestDatabase.CreateAsync();
        var coordinator = new PhysicalPassageConfirmationCoordinator(
            database.Store,
            TimeProvider.System,
            enabled: true);

        Assert.False(await coordinator.ConfirmAsync(
            new object(), "front-door", DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(0, await database.Store.CountPendingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OldSyntheticPassageAndReleaseTimeoutCannotConfirmTheAuthorization()
    {
        await using var database = await TestDatabase.CreateAsync();
        var coordinator = new PhysicalPassageConfirmationCoordinator(
            database.Store,
            TimeProvider.System,
            enabled: true);
        var board = new object();
        var authorizationTime = DateTime.UtcNow;
        Assert.True(coordinator.TryArm(board, "front-door", Guid.NewGuid(), authorizationTime));

        Assert.False(await coordinator.ConfirmAsync(
            board, "front-door", authorizationTime.AddTicks(-1), CancellationToken.None));
        coordinator.HandleReleaseTimeout(board);
        Assert.False(await coordinator.ConfirmAsync(
            board, "front-door", DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(0, await database.Store.CountPendingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SyntheticCallbacksFromConcurrentDuplicateEventsEnqueueAtMostOnePassage()
    {
        await using var database = await TestDatabase.CreateAsync();
        var coordinator = new PhysicalPassageConfirmationCoordinator(
            database.Store,
            TimeProvider.System,
            enabled: true);
        var board = new object();
        var requestId = Guid.NewGuid();
        Assert.True(coordinator.TryArm(board, "front-door", requestId, DateTime.UtcNow.AddSeconds(-1)));

        var callbacks = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            coordinator.ConfirmAsync(
                board, "front-door", DateTime.UtcNow, CancellationToken.None)));

        Assert.Equal(1, callbacks.Count(accepted => accepted));
        Assert.Equal(1, await database.Store.CountPendingAsync(CancellationToken.None));
        Assert.Equal(requestId, Assert.Single(await database.Store.GetPendingAsync(
            10, CancellationToken.None)).RequestId);
    }

    [Fact]
    public async Task AutomaticConfirmation_RemainsDisabledByDefault()
    {
        Assert.False(new ToletusLiteNet2Options().EnablePhysicalPassageConfirmation);

        await using var database = await TestDatabase.CreateAsync();
        var coordinator = new PhysicalPassageConfirmationCoordinator(
            database.Store,
            TimeProvider.System,
            enabled: false);
        var board = new object();

        Assert.False(coordinator.TryArm(
            board, "front-door", Guid.NewGuid(), DateTime.UtcNow));
        Assert.False(await coordinator.ConfirmAsync(
            board, "front-door", DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(0, await database.Store.CountPendingAsync(CancellationToken.None));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _path;

        private TestDatabase(string path)
        {
            _path = path;
            Store = new SqlitePhysicalPassageOfflineStore(Options.Create(
                new AccessOfflineStoreOptions { DatabasePath = path }));
        }

        public SqlitePhysicalPassageOfflineStore Store { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase(Path.Combine(
                Path.GetTempPath(), $"gymflow-passages-{Guid.NewGuid():N}.db"));
            await database.Store.InitializeAsync(CancellationToken.None);
            return database;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var file = _path + suffix;
                if (File.Exists(file))
                    File.Delete(file);
            }

            return ValueTask.CompletedTask;
        }
    }
}
