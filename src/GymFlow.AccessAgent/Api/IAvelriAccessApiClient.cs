using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;

namespace GymFlow.AccessAgent.Api;

public interface IAvelriAccessApiClient
{
    Task<AgentLoginResult?> LoginAsync(
        AgentCredentials credentials,
        CancellationToken cancellationToken);

    Task<AgentAccessDecisionResult> DecideAsync(
        AgentCredentials credentials,
        string token,
        string providerKey,
        DeviceAccessAttempt attempt,
        CancellationToken cancellationToken);

    Task SyncOfflineEventAsync(
        AgentCredentials credentials,
        string token,
        PendingAccessEvent accessEvent,
        CancellationToken cancellationToken);

    Task<AgentControlPlaneHeartbeatResult>
        HeartbeatAsync(
            AgentCredentials credentials,
            string token,
            long? appliedConfigurationVersion,
            CancellationToken cancellationToken);
}

public sealed record AgentLoginResult(
    Guid AgentId,
    Guid GymId,
    string Name,
    string Token);

public sealed record AgentAccessDecisionResult(
    Guid RequestId,
    bool Allowed,
    string Reason,
    DateTime ProcessedAt);

public sealed record AgentControlPlaneHeartbeatResult(
    bool ReleaseEnabled,
    long ConfigurationVersion,
    DateTime ServerTimeUtc);
