using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.IntegrationTests.Security;

public sealed class PostgresPhysicalAccessPinAttemptLimiterTests
{
    private static readonly TimeProvider PastClock = new FixedClock(
        new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly TimeProvider FutureClock = new FixedClock(
        new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [IsolatedPostgresFact]
    public Task PostgreSql_FirstFiveReservationsAreAllowedAndSixthIsDenied() =>
        WithDisposableDatabaseAsync(async database =>
        {
            var gym = Guid.NewGuid();
            var before = await database.NowAsync();
            await using var db = database.CreateContext();
            var limiter = new PersistentPhysicalAccessPinAttemptLimiter(db, PastClock);
            var reservations = new List<PhysicalAccessPinReservation>();
            for (var attempt = 0; attempt < 5; attempt++)
                reservations.Add(Assert.IsType<PhysicalAccessPinReservation>(await limiter.TryReserveAsync(gym)));

            Assert.Null(await limiter.TryReserveAsync(gym));
            var after = await database.NowAsync();
            Assert.All(reservations, reservation => Assert.Equal(reservations[0].WindowStart, reservation.WindowStart));
            // Deliberately unrelated application clocks must not control PostgreSQL's window.
            Assert.InRange(reservations[0].WindowStart, before, after);
            var state = await database.StateAsync(gym);
            Assert.Equal(0, state.Failures);
            Assert.Equal(5, state.Pending);
            Assert.Equal(0, state.LockedUntil);
        });

    [IsolatedPostgresFact]
    public Task PostgreSql_ConcurrentContextsCannotReserveMoreThanFiveSlots() =>
        WithDisposableDatabaseAsync(async database =>
        {
            var gym = Guid.NewGuid();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempts = Enumerable.Range(0, 32).Select(async instance =>
            {
                await using var db = database.CreateContext();
                var limiter = new PersistentPhysicalAccessPinAttemptLimiter(db,
                    instance % 2 == 0 ? PastClock : FutureClock);
                await start.Task;
                return await limiter.TryReserveAsync(gym);
            }).ToArray();
            start.SetResult();

            var reservations = await Task.WhenAll(attempts);
            Assert.Equal(5, reservations.Count(reservation => reservation is not null));
            Assert.Equal(27, reservations.Count(reservation => reservation is null));
            var state = await database.StateAsync(gym);
            Assert.Equal(0, state.Failures);
            Assert.Equal(5, state.Pending);
        });

    [IsolatedPostgresFact]
    public Task PostgreSql_TwoLogicalInstancesAndRestartSharePendingReservations() =>
        WithDisposableDatabaseAsync(async database =>
        {
            var gym = Guid.NewGuid();
            await using (var firstContext = database.CreateContext())
            await using (var secondContext = database.CreateContext())
            {
                var first = new PersistentPhysicalAccessPinAttemptLimiter(firstContext, PastClock);
                var second = new PersistentPhysicalAccessPinAttemptLimiter(secondContext, FutureClock);
                for (var attempt = 0; attempt < 5; attempt++)
                    Assert.NotNull(await (attempt % 2 == 0 ? first : second).TryReserveAsync(gym));

                Assert.Null(await first.TryReserveAsync(gym));
                Assert.Null(await second.TryReserveAsync(gym));
            }

            Assert.Null(await database.ReserveAsync(gym));
            Assert.Equal(5, (await database.StateAsync(gym)).Pending);
        });

    [IsolatedPostgresFact]
    public Task PostgreSql_FiveFailuresLockForFiveMinutesAcrossRestartAndWindowExpiry() =>
        WithDisposableDatabaseAsync(async database =>
        {
            var gym = Guid.NewGuid();
            var before = await database.NowAsync();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var reservation = Assert.IsType<PhysicalAccessPinReservation>(await database.ReserveAsync(gym));
                await database.CompleteAsync(reservation, failed: true);
            }

            var after = await database.NowAsync();
            var locked = await database.StateAsync(gym);
            Assert.Equal(5, locked.Failures);
            Assert.Equal(0, locked.Pending);
            Assert.InRange(locked.LockedUntil, before + 300_000, after + 300_000);
            Assert.Null(await database.ReserveAsync(gym));

            // Simulate a window older than one minute only in this disposable database.
            // The independent five-minute lock must continue to deny a recreated instance.
            await database.ExecuteAsync("""
                UPDATE "PhysicalAccessPinAttemptStates"
                SET "WindowStart" = "WindowStart" - 61_000
                WHERE "GymId" = @gymId
                """, gym);
            Assert.Null(await database.ReserveAsync(gym));
            Assert.Equal(locked.LockedUntil, (await database.StateAsync(gym)).LockedUntil);

            await database.ExecuteAsync("""
                UPDATE "PhysicalAccessPinAttemptStates"
                SET "LockedUntil" = (EXTRACT(EPOCH FROM clock_timestamp()) * 1000)::bigint - 1
                WHERE "GymId" = @gymId
                """, gym);
            Assert.NotNull(await database.ReserveAsync(gym));
            var reset = await database.StateAsync(gym);
            Assert.Equal(0, reset.Failures);
            Assert.Equal(1, reset.Pending);
            Assert.Equal(0, reset.LockedUntil);
            Assert.True(reset.WindowStart > locked.WindowStart);
        });

    [IsolatedPostgresFact]
    public Task PostgreSql_GymsHaveIndependentCountersAndLocks() =>
        WithDisposableDatabaseAsync(async database =>
        {
            var firstGym = Guid.NewGuid();
            var secondGym = Guid.NewGuid();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var reservation = Assert.IsType<PhysicalAccessPinReservation>(await database.ReserveAsync(firstGym));
                await database.CompleteAsync(reservation, failed: true);
            }

            Assert.Null(await database.ReserveAsync(firstGym));
            for (var attempt = 0; attempt < 5; attempt++)
                Assert.NotNull(await database.ReserveAsync(secondGym));
            Assert.Null(await database.ReserveAsync(secondGym));

            var first = await database.StateAsync(firstGym);
            var second = await database.StateAsync(secondGym);
            Assert.Equal(5, first.Failures);
            Assert.Equal(0, first.Pending);
            Assert.True(first.LockedUntil > await database.NowAsync());
            Assert.Equal(0, second.Failures);
            Assert.Equal(5, second.Pending);
            Assert.Equal(0, second.LockedUntil);
            await using var db = database.CreateContext();
            Assert.Equal(2, await db.PhysicalAccessPinAttemptStates.CountAsync());
        });

    [IsolatedPostgresFact]
    public Task PostgreSql_SuccessReleasesCapacityAndExpiredWindowRenewsAtomically() =>
        WithDisposableDatabaseAsync(async database =>
        {
            var gym = Guid.NewGuid();
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var reservation = Assert.IsType<PhysicalAccessPinReservation>(await database.ReserveAsync(gym));
                await database.CompleteAsync(reservation, failed: false);
            }

            var successful = await database.StateAsync(gym);
            Assert.Equal(0, successful.Failures);
            Assert.Equal(0, successful.Pending);
            Assert.Equal(0, successful.LockedUntil);

            for (var attempt = 0; attempt < 5; attempt++)
                Assert.NotNull(await database.ReserveAsync(gym));
            Assert.Null(await database.ReserveAsync(gym));
            await database.ExecuteAsync("""
                UPDATE "PhysicalAccessPinAttemptStates"
                SET "WindowStart" = "WindowStart" - 61_000
                WHERE "GymId" = @gymId
                """, gym);

            var before = await database.NowAsync();
            Assert.NotNull(await database.ReserveAsync(gym));
            var after = await database.NowAsync();
            var renewed = await database.StateAsync(gym);
            Assert.InRange(renewed.WindowStart, before, after);
            Assert.Equal(0, renewed.Failures);
            Assert.Equal(1, renewed.Pending);
            Assert.Equal(0, renewed.LockedUntil);
        });

    private static async Task WithDisposableDatabaseAsync(Func<TestDatabase, Task> test)
    {
        // Never consume application configuration or a development/production connection.
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")
            ?? throw new InvalidOperationException("The isolated PostgreSQL connection is required."));
        if (connection.Host != "127.0.0.1" || connection.Port != 55439 ||
            connection.Database != "gymflow_codex_test_base" || connection.Username != "gymflow_test")
            throw new InvalidOperationException("Only the explicitly isolated loopback PostgreSQL cluster is allowed.");
        connection.Pooling = false;
        var expectedDataDirectory = Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(expectedDataDirectory) || !Path.IsPathFullyQualified(expectedDataDirectory) ||
            !File.Exists(Path.Combine(expectedDataDirectory, "PG_VERSION")))
            throw new InvalidOperationException("The verified disposable PostgreSQL data directory is required.");

        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var verify = new NpgsqlCommand("SELECT current_setting('data_directory')", admin))
        {
            var actual = (string)(await verify.ExecuteScalarAsync())!;
            if (!string.Equals(Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(expectedDataDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PostgreSQL is not the verified disposable cluster. No test database was created.");
        }

        var databaseName = $"gymflow_codex_test_pin_{Guid.NewGuid():N}";
        try
        {
            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
                await create.ExecuteNonQueryAsync();
            connection.Database = databaseName;
            var database = new TestDatabase(connection.ConnectionString);
            await using (var setup = database.CreateContext())
            {
                await setup.Database.MigrateAsync();
                var applied = (await setup.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.Contains("20261023215000_OnePhysicalAccessPinPerStudent", applied);
                Assert.Contains("20261023223000_PersistPhysicalAccessPinAttempts", applied);
                Assert.Empty(await setup.Database.GetPendingMigrationsAsync());
            }
            await test(database);
        }
        finally
        {
            // Covers migration/setup failures as well as assertions, without retaining pooled connections.
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestDatabase(string connectionString)
    {
        private readonly DbContextOptions<AppDbContext> _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString).Options;

        public AppDbContext CreateContext() => new(_options);

        public async Task<PhysicalAccessPinReservation?> ReserveAsync(Guid gymId)
        {
            await using var db = CreateContext();
            return await new PersistentPhysicalAccessPinAttemptLimiter(db, FutureClock).TryReserveAsync(gymId);
        }

        public async Task CompleteAsync(PhysicalAccessPinReservation reservation, bool failed)
        {
            await using var db = CreateContext();
            await new PersistentPhysicalAccessPinAttemptLimiter(db, PastClock).CompleteAsync(reservation, failed);
        }

        public async Task<PhysicalAccessPinAttemptState> StateAsync(Guid gymId)
        {
            await using var db = CreateContext();
            return await db.PhysicalAccessPinAttemptStates.AsNoTracking().SingleAsync(state => state.GymId == gymId);
        }

        public async Task<long> NowAsync()
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT (EXTRACT(EPOCH FROM clock_timestamp()) * 1000)::bigint", connection);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        public async Task ExecuteAsync(string sql, Guid gymId)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("gymId", gymId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public sealed class IsolatedPostgresFactAttribute : FactAttribute
    {
        public IsolatedPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY")))
                Skip = "Requires an explicitly verified disposable loopback PostgreSQL cluster.";
        }
    }
}
