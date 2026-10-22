using GymFlow.Application.DTOs.Dashboard;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IDashboardRepository
{
    Task<OperationalDashboardSummary> GetOperationalAsync(Guid gymId, DateOnly today,
        CancellationToken cancellationToken);

    Task<DashboardSummary> GetAsync(Guid gymId, DateOnly today,
        DateTime paidFromUtc, DateTime paidUntilUtc, CancellationToken cancellationToken);
}
