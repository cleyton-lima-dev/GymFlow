using System.Net;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
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

    private readonly IAccessOfflineStore
        _offlineStore;

    private readonly IReadOnlyList<IAccessDeviceAdapter>
        _adapters;

    public AccessControlPlaneSyncService(
        IAgentCredentialStore credentialStore,
        IAvelriAccessApiClient apiClient,
        IAgentSessionService sessionService,
        IAccessReleaseControl releaseControl,
        IAccessOfflineStore offlineStore,
        IEnumerable<IAccessDeviceAdapter> adapters)
    {
        _credentialStore =
            credentialStore;

        _apiClient =
            apiClient;

        _sessionService =
            sessionService;

        _releaseControl =
            releaseControl;

        _offlineStore =
            offlineStore;

        _adapters =
            adapters.ToArray();
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

        var pendingOfflineEvents =
            await _offlineStore
                .CountPendingEventsAsync(
                    cancellationToken);

        var devices =
            _adapters
                .Select(
                    adapter =>
                        adapter.GetRuntimeStatus())
                .ToArray();

        var desiredConfiguration =
            await SendHeartbeatAsync(
                credentials,
                localConfiguration
                    .ConfigurationVersion,
                pendingOfflineEvents,
                devices,
                cancellationToken);

        if (desiredConfiguration is null)
            return false;

        ValidateConfigurationVersion(
            desiredConfiguration
                .ConfigurationVersion);

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
                desiredConfiguration
                    .ConfigurationVersion,
                pendingOfflineEvents,
                devices,
                cancellationToken);

        if (confirmation is null)
            return false;

        ValidateConfigurationVersion(
            confirmation.ConfigurationVersion);

        if (confirmation.ConfigurationVersion !=
                desiredConfiguration
                    .ConfigurationVersion ||
            confirmation.ReleaseEnabled !=
                desiredConfiguration
                    .ReleaseEnabled)
        {
            await _releaseControl
                .ApplyConfigurationAsync(
                    confirmation.ReleaseEnabled,
                    confirmation.ConfigurationVersion,
                    cancellationToken);
        }

        return true;
    }

    private async Task<
        AgentControlPlaneHeartbeatResult?>
        SendHeartbeatAsync(
            AgentCredentials credentials,
            long? appliedConfigurationVersion,
            int pendingOfflineEvents,
            IReadOnlyCollection<
                AccessDeviceRuntimeStatus> devices,
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
                    pendingOfflineEvents,
                    devices,
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
                        pendingOfflineEvents,
                        devices,
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
