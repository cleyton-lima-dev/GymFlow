using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Services;

namespace GymFlow.AccessAgent;

public class Worker : BackgroundService
{
    private readonly IEnumerable<IAccessDeviceAdapter>
        _adapters;

    private readonly IAccessDecisionService
        _accessDecisionService;

    private readonly ILogger<Worker>
        _logger;

    public Worker(
        IEnumerable<IAccessDeviceAdapter> adapters,
        IAccessDecisionService accessDecisionService,
        ILogger<Worker> logger)
    {
        _adapters = adapters;
        _accessDecisionService = accessDecisionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var adapters = _adapters.ToList();

        if (adapters.Count == 0)
        {
            _logger.LogWarning(
                "Nenhum adapter de controle de acesso está configurado.");

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);

            return;
        }

        _logger.LogInformation(
            "Avelri Access Agent iniciado com {AdapterCount} adapter(s).",
            adapters.Count);

        var tasks =
            adapters
                .Select(adapter =>
                    RunAdapterAsync(
                        adapter,
                        stoppingToken))
                .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task RunAdapterAsync(
        IAccessDeviceAdapter adapter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Iniciando adapter {ProviderKey}.",
            adapter.ProviderKey);

        await adapter.StartAsync(
            async (attempt, token) =>
            {
                var decision =
                    await _accessDecisionService.DecideAsync(
                        adapter.ProviderKey,
                        attempt,
                        token);

                await adapter.ApplyDecisionAsync(
                    attempt,
                    decision,
                    token);

                _logger.LogInformation(
                    "Decisão processada. Provider: {ProviderKey}, RequestId: {RequestId}, Allowed: {Allowed}, Reason: {Reason}",
                    adapter.ProviderKey,
                    attempt.RequestId,
                    decision.Allowed,
                    decision.Reason);

                return decision;
            },
            cancellationToken);
    }
}