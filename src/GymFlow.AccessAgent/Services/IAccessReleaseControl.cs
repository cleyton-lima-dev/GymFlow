namespace GymFlow.AccessAgent.Services;

public interface IAccessReleaseControl
{
    Task<bool> IsReleaseEnabledAsync(
        CancellationToken cancellationToken);

    Task EnableAsync(
        CancellationToken cancellationToken);

    Task DisableAsync(
        CancellationToken cancellationToken);
}
