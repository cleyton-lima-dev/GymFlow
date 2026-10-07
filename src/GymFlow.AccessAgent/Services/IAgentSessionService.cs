using GymFlow.AccessAgent.Security;

namespace GymFlow.AccessAgent.Services;

public interface IAgentSessionService
{
    Task<string?> GetTokenAsync(
        AgentCredentials credentials,
        CancellationToken cancellationToken);

    void InvalidateToken();
}