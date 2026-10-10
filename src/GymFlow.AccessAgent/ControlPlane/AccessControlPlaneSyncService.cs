using System.Net;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;

namespace GymFlow.AccessAgent.ControlPlane;

public sealed class AccessControlPlaneSyncService :
    IAccessControlPlaneSyncService
{
    private readonly IAgentCredentialStore
        _credentialStore;

    private readonly IAvelriAccessApiClient
        _apiClient;

    private readonly IAgentSessionService
        _sessionService;

    private readonly IAccessReleaseControl
        _releaseControl;

    public AccessControlPlaneSyncService(
        IAgentCredentialStore credentialStore,
        IAvelriAccessApiClient apiClient,
        IAgentSessionService sessionService,
        IAccessReleaseControl releaseControl)
    {
        _credentialStore = credentialStore;
        _apiClient = apiClient;
        _sessionService = sessionService;
        _releaseControl = releaseControl;
    }

    public async Task<bool> SyncAsync(
        CancellationToken cancellationToken)
    {
        var credentials =
            await _credentialStore.LoadAsync(
                cancellationToken);

        if (credentials is null)
            return false;

        var localConfiguration =
            await _releaseControl
                .GetConfigurationAsync(
                    cancellationToken);

        var desiredConfiguration =
            await SendHeartbeatAsync(
                credentials,
                localConfiguration.ConfigurationVersion,
                cancellationToken);

        if (desiredConfiguration is null)
            return false;

        ValidateConfigurationVersion(
            desiredConfiguration.ConfigurationVersion);

        if (Matches(
                localConfiguration,
                desiredConfiguration))
        {
            return true;
        }

        await _releaseControl
            .ApplyConfigurationAsync(
                desiredConfiguration.ReleaseEnabled,
                desiredConfiguration.ConfigurationVersion,
                cancellationToken);

        var confirmation =
            await SendHeartbeatAsync(
                credentials,
                desiredConfiguration.ConfigurationVersion,
                cancellationToken);

        if (confirmation is null)
        {
            return false;
        }

        ValidateConfigurationVersion(
            confirmation.ConfigurationVersion);

        if (confirmation.ConfigurationVersion !=
                desiredConfiguration.ConfigurationVersion ||
            confirmation.ReleaseEnabled !=
                desiredConfiguration.ReleaseEnabled)
        {
            await _releaseControl
                .ApplyConfigurationAsync(
                    confirmation.ReleaseEnabled,
                    confirmation.ConfigurationVersion,
                    cancellationToken);
        }

        return true;
    }

    private async Task<AgentControlPlaneHeartbeatResult?>
        SendHeartbeatAsync(
            AgentCredentials credentials,
            long? appliedConfigurationVersion,
            CancellationToken cancellationToken)
    {
        var token =
            await _sessionService
                .GetTokenAsync(
                    credentials,
                    cancellationToken);

        if (token is null)
            return null;

        try
        {
            return await _apiClient
                .HeartbeatAsync(
                    credentials,
                    token,
                    appliedConfigurationVersion,
                    cancellationToken);
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode ==
                  HttpStatusCode.Unauthorized)
        {
            _sessionService
                .InvalidateToken();

            token =
                await _sessionService
                    .GetTokenAsync(
                        credentials,
                        cancellationToken);

            if (token is null)
                return null;

            try
            {
                return await _apiClient
                    .HeartbeatAsync(
                        credentials,
                        token,
                        appliedConfigurationVersion,
                        cancellationToken);
            }
            catch (HttpRequestException retryException)
                when (retryException.StatusCode ==
                      HttpStatusCode.Unauthorized)
            {
                _sessionService
                    .InvalidateToken();

                return null;
            }
        }
    }

    private static bool Matches(
        AccessReleaseConfiguration local,
        AgentControlPlaneHeartbeatResult desired)
    {
        return
            local.ConfigurationVersion ==
                desired.ConfigurationVersion &&
            local.ReleaseEnabled ==
                desired.ReleaseEnabled;
    }

    private static void ValidateConfigurationVersion(
        long configurationVersion)
    {
        if (configurationVersion < 1)
        {
            throw new InvalidOperationException(
                "A API retornou uma versão de configuração inválida.");
        }
    }
}
