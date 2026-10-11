namespace GymFlow.AccessAgent.ControlPlane;

public interface IAccessControlPlaneSyncService
{
    Task<bool> SyncAsync(
        CancellationToken cancellationToken);
}
