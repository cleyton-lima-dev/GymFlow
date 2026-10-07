using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Offline;

public class OfflineEventSyncService : IOfflineEventSyncService
{
    private readonly IAgentCredentialStore _credentialStore;
    private readonly IAvelriAccessApiClient _apiClient;
    private readonly IAgentSessionService _sessionService;
    private readonly IAccessOfflineStore _offlineStore;
    private readonly AccessOfflineStoreOptions _options;

    public OfflineEventSyncService(
        IAgentCredentialStore credentialStore,
        IAvelriAccessApiClient apiClient,
        IAgentSessionService sessionService,
        IAccessOfflineStore offlineStore,
        IOptions<AccessOfflineStoreOptions> options)
    {
        _credentialStore = credentialStore;
        _apiClient = apiClient;
        _sessionService = sessionService;
        _offlineStore = offlineStore;
        _options = options.Value;
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

        var token =
            await _sessionService.GetTokenAsync(
                credentials,
                cancellationToken);

        if (token is null)
            return 0;

        var pendingEvents =
            await _offlineStore.GetPendingEventsAsync(
                _options.SyncBatchSize,
                cancellationToken);

        var syncedCount = 0;

        foreach (var accessEvent in pendingEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _apiClient.SyncOfflineEventAsync(
                    credentials,
                    token,
                    accessEvent,
                    cancellationToken);
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode ==
                      System.Net.HttpStatusCode.Unauthorized)
            {
                _sessionService.InvalidateToken();

                token =
                    await _sessionService.GetTokenAsync(
                        credentials,
                        cancellationToken);

                if (token is null)
                    break;

                try
                {
                    await _apiClient.SyncOfflineEventAsync(
                        credentials,
                        token,
                        accessEvent,
                        cancellationToken);
                }
                catch (HttpRequestException retryException)
                    when (retryException.StatusCode ==
                          System.Net.HttpStatusCode.Unauthorized)
                {
                    _sessionService.InvalidateToken();
                    break;
                }
            }
            catch (HttpRequestException)
            {
                break;
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await _offlineStore.MarkEventSyncedAsync(
                accessEvent.RequestId,
                DateTime.UtcNow,
                cancellationToken);

            syncedCount++;
        }

        return syncedCount;
    }
}