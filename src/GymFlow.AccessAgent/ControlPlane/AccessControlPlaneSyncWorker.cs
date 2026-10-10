using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.ControlPlane;

public sealed class AccessControlPlaneSyncWorker :
    BackgroundService
{
    private readonly IAccessControlPlaneSyncService
        _syncService;

    private readonly AccessControlPlaneOptions
        _options;

    private readonly ILogger<AccessControlPlaneSyncWorker>
        _logger;

    public AccessControlPlaneSyncWorker(
        IAccessControlPlaneSyncService syncService,
        IOptions<AccessControlPlaneOptions> options,
        ILogger<AccessControlPlaneSyncWorker> logger)
    {
        _syncService = syncService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var interval =
            TimeSpan.FromSeconds(
                Math.Max(
                    15,
                    _options.HeartbeatIntervalSeconds));

        var failureAlreadyLogged =
            false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var synchronized =
                    await _syncService.SyncAsync(
                        stoppingToken);

                if (!synchronized)
                {
                    if (!failureAlreadyLogged)
                    {
                        _logger.LogWarning(
                            "O plano de controle do Access Agent não pôde ser sincronizado. Verifique pareamento e autenticação.");

                        failureAlreadyLogged =
                            true;
                    }
                }
                else
                {
                    if (failureAlreadyLogged)
                    {
                        _logger.LogInformation(
                            "Sincronização do plano de controle do Access Agent restabelecida.");

                        failureAlreadyLogged =
                            false;
                    }
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException ex)
            {
                if (!failureAlreadyLogged)
                {
                    _logger.LogWarning(
                        ex,
                        "API Avelri indisponível para o plano de controle. O Agent continuará usando a última configuração local válida.");

                    failureAlreadyLogged =
                        true;
                }
            }
            catch (Exception ex)
            {
                if (!failureAlreadyLogged)
                {
                    _logger.LogError(
                        ex,
                        "Falha inesperada na sincronização do plano de controle do Access Agent.");

                    failureAlreadyLogged =
                        true;
                }
            }

            try
            {
                await Task.Delay(
                    interval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
