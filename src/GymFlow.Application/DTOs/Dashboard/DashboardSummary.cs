namespace GymFlow.Application.DTOs.Dashboard;

public sealed record DashboardSummary(
    DateOnly Today,
    DateOnly PeriodStart,
    DateOnly PeriodEndExclusive,
    int ExpiringWithinDays,
    decimal ExpectedRevenue,
    decimal ReceivedRevenue,
    int OverdueCharges,
    decimal OverdueAmount,
    int ActiveEnrollments,
    int ExpiringEnrollments,
    int ActiveStudents,
    int StudentsWithoutAssessment,
    int ReassessmentsDue);
