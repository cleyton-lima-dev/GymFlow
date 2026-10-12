namespace GymFlow.AccessAgent.Offline;

public interface IPhysicalPassageSyncService
{
    Task<int> SyncPendingAsync(
        CancellationToken cancellationToken);
}