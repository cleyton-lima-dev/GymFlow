using GymFlow.AccessAgent.Services;

namespace GymFlow.AccessAgent.Offline;

public class OfflineSyncWorker :
    BackgroundService
{
    private static readonly TimeSpan SyncInterval =
        TimeSpan.FromSeconds(10);

    private readonly IOfflineEventSyncService
        _syncService;

    private readonly IAccessOperationalHealthStore
        _operationalHealthStore;

    private readonly ILogger<OfflineSyncWorker>
        _logger;

    public OfflineSyncWorker(
        IOfflineEventSyncService syncService,
        IAccessOperationalHealthStore operationalHealthStore,
        ILogger<OfflineSyncWorker> logger)
    {
        _syncService =
            syncService;

        _operationalHealthStore =
            operationalHealthStore;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(
                SyncInterval);

        while (!stoppingToken
                   .IsCancellationRequested)
        {
            try
            {
                var syncedCount =
                    await _syncService
                        .SyncPendingAsync(
                            stoppingToken);

                if (syncedCount > 0)
                {
                    _logger.LogInformation(
                        "{SyncedCount} evento(s) offline sincronizado(s).",
                        syncedCount);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken
                    .IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _operationalHealthStore
                    .RecordFailure(
                        "OfflineSync.Unexpected",
                        DateTime.UtcNow);

                _logger.LogError(
                    ex,
                    "Falha inesperada ao sincronizar eventos offline.");
            }

            try
            {
                await timer
                    .WaitForNextTickAsync(
                        stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken
                    .IsCancellationRequested)
            {
                break;
            }
        }
    }
}
