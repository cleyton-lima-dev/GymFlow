using GymFlow.Application.DTOs.Dashboard;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IDashboardRepository
{
    Task<DashboardSummary> GetAsync(Guid gymId, DateOnly today,
        DateTime paidFromUtc, DateTime paidUntilUtc, CancellationToken cancellationToken);
}
