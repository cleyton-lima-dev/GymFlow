namespace GymFlow.Application.DTOs.Reports;

public sealed class ReportQuery
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? StudentId { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? EnrollmentStatus { get; set; }
    public string View { get; set; } = "charges";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed record ReportResult(string Title, string PeriodBasis, string TimeZone,
    DateOnly? FromDate, DateOnly? ToDate, string[] Columns, List<string[]> Rows,
    int TotalCount, int Page, int PageSize, Dictionary<string, string> Totals);

public sealed record ReportContext(DateOnly Today, TimeZoneInfo Zone, DateTime FromUtc, DateTime UntilUtc,
    bool IncludeEnrollmentMetadata = true);
