using GymFlow.Application.DTOs.Dashboard;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;

namespace GymFlow.Application.Services;

public sealed class DashboardService(
    IDashboardRepository repository, IGymTimeZoneProvider timeZones, TimeProvider clock)
{
    public Task<DashboardSummary> GetAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        var zone = timeZones.GetTimeZone(gymId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var start = new DateOnly(today.Year, today.Month, 1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), zone);
        var untilUtc = TimeZoneInfo.ConvertTimeToUtc(start.AddMonths(1).ToDateTime(TimeOnly.MinValue), zone);
        return repository.GetAsync(gymId, today, fromUtc, untilUtc, cancellationToken);
    }
}
