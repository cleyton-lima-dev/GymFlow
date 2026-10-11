using GymFlow.AccessAgent.Services;

namespace GymFlow.AccessAgent.Offline;

public class OfflineSyncWorker :
    BackgroundService
{
    private static readonly TimeSpan SyncInterval =
        TimeSpan.FromSeconds(10);

    private readonly IOfflineEventSyncService
        _eventSyncService;

    private readonly IPhysicalPassageSyncService
        _passageSyncService;

    private readonly IAccessOperationalHealthStore
        _operationalHealthStore;

    private readonly ILogger<OfflineSyncWorker>
        _logger;

    public OfflineSyncWorker(
        IOfflineEventSyncService eventSyncService,
        IPhysicalPassageSyncService passageSyncService,
        IAccessOperationalHealthStore operationalHealthStore,
        ILogger<OfflineSyncWorker> logger)
    {
        _eventSyncService =
            eventSyncService;

        _passageSyncService =
            passageSyncService;

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
                var syncedEvents =
                    await _eventSyncService
                        .SyncPendingAsync(
                            stoppingToken);

                if (syncedEvents > 0)
                {
                    _logger.LogInformation(
                        "{SyncedCount} evento(s) offline sincronizado(s).",
                        syncedEvents);
                }

                var syncedPassages =
                    await _passageSyncService
                        .SyncPendingAsync(
                            stoppingToken);

                if (syncedPassages > 0)
                {
                    _logger.LogInformation(
                        "{SyncedCount} passagem(ns) física(s) sincronizada(s).",
                        syncedPassages);
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
                    "Falha inesperada ao sincronizar dados offline.");
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