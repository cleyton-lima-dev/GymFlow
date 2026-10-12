using System.Net;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Offline;

public sealed class PhysicalPassageSyncService :
    IPhysicalPassageSyncService
{
    private readonly IAgentCredentialStore
        _credentialStore;

    private readonly IAvelriAccessApiClient
        _apiClient;

    private readonly IAgentSessionService
        _sessionService;

    private readonly IPhysicalPassageOfflineStore
        _passageStore;

    private readonly IAccessOperationalHealthStore
        _operationalHealthStore;

    private readonly AccessOfflineStoreOptions
        _options;

    public PhysicalPassageSyncService(
        IAgentCredentialStore credentialStore,
        IAvelriAccessApiClient apiClient,
        IAgentSessionService sessionService,
        IPhysicalPassageOfflineStore passageStore,
        IAccessOperationalHealthStore operationalHealthStore,
        IOptions<AccessOfflineStoreOptions> options)
    {
        _credentialStore =
            credentialStore;

        _apiClient =
            apiClient;

        _sessionService =
            sessionService;

        _passageStore =
            passageStore;

        _operationalHealthStore =
            operationalHealthStore;

        _options =
            options.Value;
    }

    public async Task<int> SyncPendingAsync(
        CancellationToken cancellationToken)
    {
        if (_options.SyncBatchSize <= 0)
            return 0;

        var credentials =
            await _credentialStore.LoadAsync(
                cancellationToken);

        if (credentials is null)
            return 0;

        var pending =
            await _passageStore.GetPendingAsync(
                _options.SyncBatchSize,
                cancellationToken);

        if (pending.Count == 0)
            return 0;

        var token =
            await _sessionService.GetTokenAsync(
                credentials,
                cancellationToken);

        if (token is null)
        {
            RecordFailure(
                "PhysicalPassageSync.AuthenticationUnavailable");

            return 0;
        }

        var syncedCount = 0;

        foreach (var passage in pending)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                await SendAsync(
                    credentials,
                    token,
                    passage,
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
                {
                    RecordFailure(
                        "PhysicalPassageSync.AuthenticationUnavailable");

                    break;
                }

                try
                {
                    await SendAsync(
                        credentials,
                        token,
                        passage,
                        cancellationToken);
                }
                catch (HttpRequestException retryException)
                    when (retryException.StatusCode ==
                        HttpStatusCode.Unauthorized)
                {
                    _sessionService
                        .InvalidateToken();

                    RecordFailure(
                        "PhysicalPassageSync.Unauthorized");

                    break;
                }
                catch (HttpRequestException retryException)
                    when (retryException.StatusCode ==
                        HttpStatusCode.NotFound)
                {
                    RecordFailure(
                        "PhysicalPassageSync.AccessEventNotAvailable");

                    break;
                }
                catch (HttpRequestException)
                {
                    RecordFailure(
                        "PhysicalPassageSync.ApiUnavailable");

                    break;
                }
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode ==
                    HttpStatusCode.NotFound)
            {
                RecordFailure(
                    "PhysicalPassageSync.AccessEventNotAvailable");

                break;
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode ==
                    HttpStatusCode.Conflict)
            {
                RecordFailure(
                    "PhysicalPassageSync.Conflict");

                break;
            }
            catch (HttpRequestException)
            {
                RecordFailure(
                    "PhysicalPassageSync.ApiUnavailable");

                break;
            }
            catch (OperationCanceledException)
                when (!cancellationToken
                    .IsCancellationRequested)
            {
                RecordFailure(
                    "PhysicalPassageSync.Timeout");

                break;
            }

            await _passageStore.MarkSyncedAsync(
                passage.RequestId,
                DateTime.UtcNow,
                cancellationToken);

            syncedCount++;
        }

        return syncedCount;
    }

    private async Task SendAsync(
        AgentCredentials credentials,
        string token,
        PendingPhysicalPassage passage,
        CancellationToken cancellationToken)
    {
        await _apiClient
            .ConfirmPhysicalPassageAsync(
                credentials,
                token,
                passage.RequestId,
                passage.OccurredAt,
                cancellationToken);
    }

    private void RecordFailure(
        string code)
    {
        _operationalHealthStore.RecordFailure(
            code,
            DateTime.UtcNow);
    }
}