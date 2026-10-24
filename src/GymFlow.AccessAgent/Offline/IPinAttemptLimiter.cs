namespace GymFlow.AccessAgent.Offline;

public interface IPinAttemptLimiter
{
    Task<bool> IsLockedAsync(Guid gymId, CancellationToken cancellationToken);
    Task<bool> TryConsumeAsync(Guid gymId, CancellationToken cancellationToken);
}
