using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public sealed class PersistentPhysicalAccessPinAttemptLimiterTests
{
    [Fact]
    public async Task ConcurrentApiInstancesShareAtomicReservationsAndRestartedLockout()
    {
        var connectionString = $"Data Source=pin-limiter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString).Options;
        await using (var setup = new AppDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var clock = new MutableClock();
        var gym = Guid.NewGuid();
        var otherGym = Guid.NewGuid();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = new AppDbContext(options);
            var limiter = new PersistentPhysicalAccessPinAttemptLimiter(db, clock);
            return await limiter.TryReserveAsync(gym);
        }));

        var granted = attempts.Where(item => item is not null).ToArray();
        Assert.Equal(5, granted.Length);
        Assert.Equal(15, attempts.Count(item => item is null));

        await Task.WhenAll(granted.Select(async item =>
        {
            await using var db = new AppDbContext(options);
            await new PersistentPhysicalAccessPinAttemptLimiter(db, clock)
                .CompleteAsync(item!, failed: true);
        }));

        await using (var restarted = new AppDbContext(options))
        {
            var limiter = new PersistentPhysicalAccessPinAttemptLimiter(restarted, clock);
            Assert.Null(await limiter.TryReserveAsync(gym));
            Assert.NotNull(await limiter.TryReserveAsync(otherGym));
        }

        clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        await using (var restarted = new AppDbContext(options))
            Assert.NotNull(await new PersistentPhysicalAccessPinAttemptLimiter(restarted, clock)
                .TryReserveAsync(gym));
    }

    [Fact]
    public async Task SuccessfulPinsReleaseCapacityWithoutCountingAsGuesses()
    {
        var connectionString = $"Data Source=pin-limiter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString).Options;
        await using (var setup = new AppDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var gym = Guid.NewGuid();
        var clock = new MutableClock();
        for (var i = 0; i < 12; i++)
        {
            await using var db = new AppDbContext(options);
            var limiter = new PersistentPhysicalAccessPinAttemptLimiter(db, clock);
            var reservation = await limiter.TryReserveAsync(gym);
            Assert.NotNull(reservation);
            await limiter.CompleteAsync(reservation, failed: false);
        }
    }

    private sealed class MutableClock : TimeProvider
    {
        private long _milliseconds = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Read(ref _milliseconds));
        public void Advance(TimeSpan duration) =>
            Interlocked.Add(ref _milliseconds, (long)duration.TotalMilliseconds);
    }
}
