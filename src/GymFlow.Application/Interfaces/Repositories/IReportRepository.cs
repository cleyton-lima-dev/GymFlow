using GymFlow.Application.DTOs.Reports;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IReportRepository
{
    Task<ReportResult> GetAsync(Guid gymId, string kind, ReportQuery query, ReportContext context,
        bool export, CancellationToken cancellationToken);
}
