using GymFlow.AccessAgent.Offline;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Tests.Offline;

public class SqlitePinAttemptLimiterTests
{
    [Fact]
    public async Task ConcurrentAttemptsCannotExceedWindowPermitCount()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avelri-pin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var limiter = new SqlitePinAttemptLimiter(
                Options.Create(new AccessOfflineStoreOptions
                {
                    DatabasePath = Path.Combine(directory, "agent-test.db")
                }), new MutableClock());
            var gym = Guid.NewGuid();
            var results = await Task.WhenAll(Enumerable.Range(0, 20)
                .Select(_ => limiter.TryConsumeAsync(gym, CancellationToken.None)));
            Assert.Equal(5, results.Count(allowed => allowed));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AttemptsSurviveLimiterRestartAndRemainIsolatedByGym()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avelri-pin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var options = Options.Create(new AccessOfflineStoreOptions
            {
                DatabasePath = Path.Combine(directory, "agent-test.db")
            });
            var clock = new MutableClock();
            var gym = Guid.NewGuid();
            var otherGym = Guid.NewGuid();
            var firstInstance = new SqlitePinAttemptLimiter(options, clock);

            for (var attempt = 0; attempt < 5; attempt++)
                Assert.True(await firstInstance.TryConsumeAsync(gym, CancellationToken.None));

            var restarted = new SqlitePinAttemptLimiter(options, clock);
            Assert.False(await restarted.TryConsumeAsync(gym, CancellationToken.None));
            Assert.True(await restarted.TryConsumeAsync(otherGym, CancellationToken.None));
            clock.Advance(TimeSpan.FromMinutes(4));
            Assert.False(await restarted.TryConsumeAsync(gym, CancellationToken.None));
            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.True(await restarted.TryConsumeAsync(gym, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
