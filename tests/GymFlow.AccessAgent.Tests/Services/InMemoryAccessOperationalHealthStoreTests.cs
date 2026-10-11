using GymFlow.AccessAgent.Services;

namespace GymFlow.AccessAgent.Tests.Services;

public class InMemoryAccessOperationalHealthStoreTests
{
    [Fact]
    public void Snapshot_ShouldKeepNewestOperationalTimestamps()
    {
        var store =
            new InMemoryAccessOperationalHealthStore();

        var now =
            DateTime.UtcNow;

        store.RecordOfflineSync(
            now);

        store.RecordOfflineSync(
            now.AddMinutes(-1));

        store.RecordFailure(
            "OfflineSync.ApiUnavailable",
            now.AddMinutes(1));

        store.RecordFailure(
            "OlderFailure",
            now);

        var snapshot =
            store.GetSnapshot();

        Assert.Equal(
            now,
            snapshot.LastOfflineSyncAt);

        Assert.Equal(
            now.AddMinutes(1),
            snapshot.LastFailureAt);

        Assert.Equal(
            "OfflineSync.ApiUnavailable",
            snapshot.LastFailureCode);
    }
}
