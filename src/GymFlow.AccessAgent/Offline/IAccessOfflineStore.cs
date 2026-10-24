using GymFlow.AccessAgent.Abstractions;

namespace GymFlow.AccessAgent.Offline;

public interface IAccessOfflineStore
{
    Task InitializeAsync(
        CancellationToken cancellationToken);

    Task<CachedAccessPermission?> GetPermissionAsync(
        string providerKey,
        AccessCredentialType credentialType,
        string externalIdentifier,
        DateTime utcNow,
        CancellationToken cancellationToken,
        Guid gymId = default,
        Guid agentId = default,
        string deviceKey = "");

    Task UpsertPermissionAsync(
        CachedAccessPermission permission,
        CancellationToken cancellationToken);

    Task EnqueueEventAsync(
        PendingAccessEvent accessEvent,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingAccessEvent>>
        GetPendingEventsAsync(
            int limit,
            CancellationToken cancellationToken);

    Task<int> CountPendingEventsAsync(
        CancellationToken cancellationToken);

    Task MarkEventSyncedAsync(
        Guid requestId,
        DateTime syncedAt,
        CancellationToken cancellationToken);
}

public sealed record CachedAccessPermission(
    string ProviderKey,
    AccessCredentialType CredentialType,
    string ExternalIdentifier,
    bool Allowed,
    string Reason,
    DateTime ValidUntil,
    DateTime UpdatedAt,
    Guid GymId = default,
    Guid AgentId = default,
    string DeviceKey = "");

public sealed record PendingAccessEvent(
    Guid RequestId,
    string ProviderKey,
    AccessCredentialType CredentialType,
    string ExternalIdentifier,
    DateTime OccurredAt,
    bool Allowed,
    string Reason,
    AccessDecisionSource Source,
    DateTime CreatedAt,
    DateTime? SyncedAt,
    string DeviceKey = "",
    Guid? AgentId = null);

public enum AccessDecisionSource
{
    Online = 1,
    OfflineCache = 2,
    OfflineFailClosed = 3
}
