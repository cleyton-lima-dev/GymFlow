namespace GymFlow.AccessAgent.Services;

public interface IAccessOperationalHealthStore
{
    AccessOperationalHealthSnapshot GetSnapshot();

    void RecordOfflineSync(
        DateTime occurredAtUtc);

    void RecordFailure(
        string code,
        DateTime occurredAtUtc);
}

public sealed record AccessOperationalHealthSnapshot(
    DateTime? LastOfflineSyncAt,
    DateTime? LastFailureAt,
    string? LastFailureCode);
