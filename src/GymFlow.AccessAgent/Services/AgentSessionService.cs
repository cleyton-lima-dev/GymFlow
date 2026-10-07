using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;

namespace GymFlow.AccessAgent.Services;

public class AgentSessionService : IAgentSessionService
{
    private readonly IAvelriAccessApiClient _apiClient;

    private readonly SemaphoreSlim _authenticationLock =
        new(1, 1);

    private string? _token;

    public AgentSessionService(
        IAvelriAccessApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<string?> GetTokenAsync(
        AgentCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_token))
            return _token;

        await _authenticationLock.WaitAsync(
            cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(_token))
                return _token;

            var login =
                await _apiClient.LoginAsync(
                    credentials,
                    cancellationToken);

            _token = login?.Token;

            return _token;
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    public void InvalidateToken()
    {
        _token = null;
    }
}