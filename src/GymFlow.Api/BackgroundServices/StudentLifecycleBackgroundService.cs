using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;

namespace GymFlow.Api.BackgroundServices;

public sealed class StudentLifecycleBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StudentLifecycleBackgroundService> _logger;

    public StudentLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StudentLifecycleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>(
            "StudentLifecycle:Enabled");

        if (!enabled)
        {
            _logger.LogInformation(
                "Student lifecycle background service is disabled.");

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var studentRepository =
                    scope.ServiceProvider
                        .GetRequiredService<IStudentRepository>();

                var lifecycleService =
                    scope.ServiceProvider
                        .GetRequiredService<StudentLifecycleService>();

                var gymIds =
                    await studentRepository
                        .GetGymIdsForLifecycleAsync();

                foreach (var gymId in gymIds)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await lifecycleService.ProcessAsync(gymId);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error while processing student lifecycle.");
            }

            await Task.Delay(
                TimeSpan.FromHours(1),
                stoppingToken);
        }
    }
}