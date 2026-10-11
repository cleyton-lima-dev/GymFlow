namespace GymFlow.AccessAgent.Offline;

public interface IOfflineEventSyncService
{
    Task<int> SyncPendingAsync(
        CancellationToken cancellationToken);
}