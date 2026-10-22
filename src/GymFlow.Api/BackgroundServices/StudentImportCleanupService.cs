using GymFlow.Api.Services;

namespace GymFlow.Api.BackgroundServices;

public sealed class StudentImportCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<StudentImportCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var imports = scope.ServiceProvider.GetRequiredService<StudentImportService>();
                await imports.RemoveExpiredPreviewsAsync(stoppingToken);
                var migrations = scope.ServiceProvider.GetRequiredService<MigrationService>();
                await migrations.RemoveExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Import previews contain personal data; never include row data in logs.
                logger.LogWarning("A limpeza de prévias expiradas de importação falhou.");
            }
        }
    }
}
