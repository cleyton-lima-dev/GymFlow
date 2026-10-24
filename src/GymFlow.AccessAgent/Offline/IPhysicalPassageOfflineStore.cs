namespace GymFlow.AccessAgent.Offline;

public interface IPhysicalPassageOfflineStore
{
    Task InitializeAsync(
        CancellationToken cancellationToken);

    Task EnqueueAsync(
        PendingPhysicalPassage passage,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingPhysicalPassage>>
        GetPendingAsync(
            int limit,
            CancellationToken cancellationToken);

    Task<int> CountPendingAsync(
        CancellationToken cancellationToken);

    Task MarkSyncedAsync(
        Guid requestId,
        DateTime syncedAt,
        CancellationToken cancellationToken);
}

public sealed record PendingPhysicalPassage(
    Guid RequestId,
    DateTime OccurredAt,
    DateTime CreatedAt,
    DateTime? SyncedAt,
    string DeviceKey = "");
