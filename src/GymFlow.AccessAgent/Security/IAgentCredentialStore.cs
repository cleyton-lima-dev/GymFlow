namespace GymFlow.AccessAgent.Security;

public interface IAgentCredentialStore
{
    Task SaveAsync(
        AgentCredentials credentials,
        CancellationToken cancellationToken);

    Task<AgentCredentials?> LoadAsync(
        CancellationToken cancellationToken);
}

public sealed record AgentCredentials(
    Guid AgentId,
    Guid GymId,
    string Secret,
    string ApiBaseUrl);