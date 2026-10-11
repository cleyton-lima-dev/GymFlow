using GymFlow.AccessAgent.Abstractions;

namespace GymFlow.AccessAgent.Services;

public interface IAccessDecisionService
{
    Task<AccessDeviceDecision> DecideAsync(
        string providerKey,
        DeviceAccessAttempt attempt,
        CancellationToken cancellationToken);
}