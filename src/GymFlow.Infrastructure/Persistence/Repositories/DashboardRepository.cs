using GymFlow.Application.DTOs.Dashboard;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

// Read model over the source modules: no state transitions, joins to audit, or entity loading.
public sealed class DashboardRepository(AppDbContext context) : IDashboardRepository
{
    public async Task<DashboardSummary> GetAsync(Guid gymId, DateOnly today,
        DateTime paidFromUtc, DateTime paidUntilUtc, CancellationToken cancellationToken)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(1);
        var expiring = today.AddDays(15);
        var charges = context.Charges.AsNoTracking().Where(c =>
            c.Enrollment.Student.User.GymId == gymId && c.Enrollment.Plan.GymId == gymId);
        // Expected: non-cancelled charges due this month, net of recorded discounts.
        // Received: actual PaidAmount paid this local month, independently of due date.
        var money = await charges.GroupBy(_ => 1).Select(g => new
        {
            Expected = g.Sum(c => c.Status != ChargeStatus.Cancelled && c.DueDate >= start && c.DueDate < end
                ? c.Amount - c.DiscountAmount : 0m),
            Received = g.Sum(c => c.Status == ChargeStatus.Paid && c.PaidAt >= paidFromUtc && c.PaidAt < paidUntilUtc
                ? c.PaidAmount ?? 0m : 0m),
            Overdue = g.Count(c => c.Status == ChargeStatus.Overdue ||
                (c.Status == ChargeStatus.Pending && c.DueDate < today)),
            OverdueAmount = g.Sum(c => c.Status == ChargeStatus.Overdue ||
                (c.Status == ChargeStatus.Pending && c.DueDate < today) ? c.Amount - c.DiscountAmount : 0m)
        }).SingleOrDefaultAsync(cancellationToken);

        var enrollments = await context.Enrollments.AsNoTracking().Where(e =>
                e.Student.User.GymId == gymId && e.Plan.GymId == gymId &&
                e.Status == EnrollmentStatus.Active && e.StartDate <= today && e.EndDate > today)
            .GroupBy(_ => 1).Select(g => new
            {
                Active = g.Count(),
                Expiring = g.Count(e => e.EndDate <= expiring)
            }).SingleOrDefaultAsync(cancellationToken);

        // Inverse of AssessmentDate.AddMonths(2) <= today, including month-end clamping.
        var assessmentCutoff = today.AddMonths(-2);
        if (today.Day == DateTime.DaysInMonth(today.Year, today.Month))
            assessmentCutoff = new DateOnly(assessmentCutoff.Year, assessmentCutoff.Month,
                DateTime.DaysInMonth(assessmentCutoff.Year, assessmentCutoff.Month));
        var assessments = context.PhysicalAssessments;
        var students = await context.Students.AsNoTracking().Where(s =>
                s.User.GymId == gymId && s.User.IsActive && s.ArchivedAt == null)
            .Select(s => new
            {
                Latest = assessments.Where(a => a.StudentId == s.Id)
                    .Select(a => (DateOnly?)a.AssessmentDate).Max()
            }).GroupBy(_ => 1).Select(g => new
            {
                Active = g.Count(),
                Missing = g.Count(s => s.Latest == null),
                Due = g.Count(s => s.Latest != null && s.Latest <= assessmentCutoff)
            }).SingleOrDefaultAsync(cancellationToken);

        return new(today, start, end, 15, money?.Expected ?? 0, money?.Received ?? 0,
            money?.Overdue ?? 0, money?.OverdueAmount ?? 0, enrollments?.Active ?? 0,
            enrollments?.Expiring ?? 0, students?.Active ?? 0, students?.Missing ?? 0, students?.Due ?? 0);
    }
}
