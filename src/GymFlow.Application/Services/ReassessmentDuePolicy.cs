namespace GymFlow.Application.Services;

public static class ReassessmentDuePolicy
{
    // Keep this inverse aligned with PhysicalAssessmentService's
    // AssessmentDate.AddMonths(2) <= today rule.
    public static DateOnly GetLastDueAssessmentDate(DateOnly today)
    {
        var cutoff = today.AddMonths(-2);
        if (today.Day == DateTime.DaysInMonth(today.Year, today.Month))
        {
            cutoff = new DateOnly(
                cutoff.Year,
                cutoff.Month,
                DateTime.DaysInMonth(cutoff.Year, cutoff.Month));
        }

        return cutoff;
    }
}
