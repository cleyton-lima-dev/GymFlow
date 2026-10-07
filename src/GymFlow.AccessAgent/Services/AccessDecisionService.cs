using System.Net;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Services;

public class AccessDecisionService : IAccessDecisionService
{
    private readonly IAgentCredentialStore _credentialStore;
    private readonly IAvelriAccessApiClient _apiClient;
    private readonly IAccessOfflineStore _offlineStore;
    private readonly AccessOfflineStoreOptions _offlineOptions;

    private readonly IAgentSessionService _sessionService;

    public AccessDecisionService(
    IAgentCredentialStore credentialStore,
    IAvelriAccessApiClient apiClient,
    IAccessOfflineStore offlineStore,
    IOptions<AccessOfflineStoreOptions> offlineOptions,
    IAgentSessionService sessionService)
    {
        _credentialStore = credentialStore;
        _apiClient = apiClient;
        _offlineStore = offlineStore;
        _offlineOptions = offlineOptions.Value;
        _sessionService = sessionService;
    }

    public async Task<AccessDeviceDecision> DecideAsync(
        string providerKey,
        DeviceAccessAttempt attempt,
        CancellationToken cancellationToken)
    {
        var credentials =
            await _credentialStore.LoadAsync(
                cancellationToken);

        if (credentials is null)
        {
            return new AccessDeviceDecision(
                false,
                "AgentNotPaired");
        }

        try
        {
            var token =
                await _sessionService.GetTokenAsync(
                credentials,
                cancellationToken);

            if (token is null)
            {
                return new AccessDeviceDecision(
                    false,
                    "AgentAuthenticationFailed");
            }

            AccessDeviceDecision decision;

            try
            {
                decision =
                    await SendDecisionAsync(
                        credentials,
                        token,
                        providerKey,
                        attempt,
                        cancellationToken);
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode ==
                      HttpStatusCode.Unauthorized)
            {
                _sessionService.InvalidateToken();

                token =
                    await _sessionService.GetTokenAsync(
                        credentials,
                        cancellationToken);

                if (token is null)
                {
                    return new AccessDeviceDecision(
                        false,
                        "AgentAuthenticationFailed");
                }

                try
                {
                    decision =
                        await SendDecisionAsync(
                            credentials,
                            token,
                            providerKey,
                            attempt,
                            cancellationToken);
                }
                catch (HttpRequestException retryException)
                    when (retryException.StatusCode ==
                          HttpStatusCode.Unauthorized)
                {
                    _sessionService.InvalidateToken();

                    return new AccessDeviceDecision(
                        false,
                        "AgentAuthenticationFailed");
                }
            }

            await CacheOnlineDecisionAsync(
                providerKey,
                attempt,
                decision,
                cancellationToken);

            return decision;
        }
        catch (HttpRequestException ex)
            when (IsConnectivityFailure(ex))
        {
            return await DecideOfflineAsync(
                providerKey,
                attempt,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return await DecideOfflineAsync(
                providerKey,
                attempt,
                cancellationToken);
        }
    }


    private async Task<AccessDeviceDecision> SendDecisionAsync(
        AgentCredentials credentials,
        string token,
        string providerKey,
        DeviceAccessAttempt attempt,
        CancellationToken cancellationToken)
    {
        var result =
            await _apiClient.DecideAsync(
                credentials,
                token,
                providerKey,
                attempt,
                cancellationToken);

        return new AccessDeviceDecision(
            result.Allowed,
            result.Reason);
    }

    private async Task CacheOnlineDecisionAsync(
        string providerKey,
        DeviceAccessAttempt attempt,
        AccessDeviceDecision decision,
        CancellationToken cancellationToken)
    {
        if (_offlineOptions.PermissionCacheMinutes <= 0)
            return;

        var now = DateTime.UtcNow;

        var permission =
            new CachedAccessPermission(
                providerKey,
                attempt.CredentialType,
                attempt.ExternalIdentifier,
                decision.Allowed,
                decision.Reason,
                now.AddMinutes(
                    _offlineOptions.PermissionCacheMinutes),
                now);

        await _offlineStore.UpsertPermissionAsync(
            permission,
            cancellationToken);
    }

    private async Task<AccessDeviceDecision> DecideOfflineAsync(
        string providerKey,
        DeviceAccessAttempt attempt,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var cachedPermission =
            await _offlineStore.GetPermissionAsync(
                providerKey,
                attempt.CredentialType,
                attempt.ExternalIdentifier,
                now,
                cancellationToken);

        var decision =
            cachedPermission is null
                ? new AccessDeviceDecision(
                    false,
                    "OfflineNoCachedPermission")
                : new AccessDeviceDecision(
                    cachedPermission.Allowed,
                    cachedPermission.Reason);

        var pendingEvent =
            new PendingAccessEvent(
                attempt.RequestId,
                providerKey,
                attempt.CredentialType,
                attempt.ExternalIdentifier,
                attempt.OccurredAt,
                decision.Allowed,
                decision.Reason,
                cachedPermission is null
                    ? AccessDecisionSource.OfflineFailClosed
                    : AccessDecisionSource.OfflineCache,
                 now,
                 null);

        await _offlineStore.EnqueueEventAsync(
            pendingEvent,
            cancellationToken);

        return decision;
    }

    private static bool IsConnectivityFailure(
        HttpRequestException exception)
    {
        return exception.StatusCode is null ||
               exception.StatusCode ==
                   HttpStatusCode.RequestTimeout ||
               exception.StatusCode ==
                   HttpStatusCode.BadGateway ||
               exception.StatusCode ==
                   HttpStatusCode.ServiceUnavailable ||
               exception.StatusCode ==
                   HttpStatusCode.GatewayTimeout;
    }
}