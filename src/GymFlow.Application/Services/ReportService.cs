using System.Globalization;
using System.Text;
using GymFlow.Application.DTOs.Reports;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;

namespace GymFlow.Application.Services;

public sealed class ReportService(IReportRepository repository, IGymTimeZoneProvider zones, TimeProvider clock)
{
    public const int ExportLimit = 10000;
    public static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Dictionary<string, string[]> Statuses = new()
    {
        ["students"] = ["active", "inactive", "archived"],
        ["financial"] = ["pending", "overdue", "paid", "cancelled"],
        ["enrollments"] = ["active", "future", "expired", "pending-payment", "cancelled"],
        ["attendance"] = ["manual", "physical"],
        ["access-events"] = ["allowed", "denied"]
    };

    public Task<ReportResult> GetAsync(Guid gymId, string kind, ReportQuery query, bool export = false,
        CancellationToken cancellationToken = default, bool includeEnrollmentMetadata = true)
    {
        if (gymId == Guid.Empty || !Statuses.TryGetValue(kind, out var statuses))
            throw new ArgumentException("Relatório inválido.");
        query.Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status;
        query.Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        query.EnrollmentStatus = string.IsNullOrWhiteSpace(query.EnrollmentStatus) ? null : query.EnrollmentStatus;
        if (kind == "students" && !includeEnrollmentMetadata && query.EnrollmentStatus != null)
            throw new ArgumentException("O filtro de matrícula exige acesso ao ERP.");
        if ((query.Status != null && !statuses.Contains(query.Status)) ||
            (query.EnrollmentStatus != null && (kind != "students" ||
                !new[] { "valid", "none" }.Contains(query.EnrollmentStatus))) ||
            (query.View != "charges" && (kind != "financial" ||
                !new[] { "payments", "overdue", "revenue" }.Contains(query.View))))
            throw new ArgumentException("Filtro de situação inválido.");
        if (query.StudentId == Guid.Empty || query.Search?.Length > 100 || query.Page < 1 ||
            query.Page > 100000 || query.PageSize < 1 || query.PageSize > 100)
            throw new ArgumentException("Filtros ou paginação inválidos.");

        var zone = zones.GetTimeZone(gymId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        if (kind == "students" && (query.FromDate.HasValue || query.ToDate.HasValue))
            throw new ArgumentException("O relatório cadastral não utiliza período.");
        if (kind != "students")
        {
            if (query.FromDate.HasValue != query.ToDate.HasValue)
                throw new ArgumentException("Informe o início e o fim do período.");
            query.FromDate ??= new DateOnly(today.Year, today.Month, 1);
            if (query.FromDate.Value.Year == 9999 && query.FromDate.Value.Month == 12)
                throw new ArgumentException("Período fora do intervalo suportado.");
            query.ToDate ??= query.FromDate.Value.AddMonths(1).AddDays(-1);
            if (query.FromDate > query.ToDate || query.ToDate == DateOnly.MaxValue ||
                query.ToDate.Value.DayNumber - query.FromDate.Value.DayNumber > 365)
                throw new ArgumentException("O período deve ter no máximo 366 dias e início anterior ao fim.");
        }
        var from = (query.FromDate ?? today).ToDateTime(TimeOnly.MinValue);
        var until = (query.ToDate ?? today).AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (zone.IsInvalidTime(from) || zone.IsInvalidTime(until))
            throw new ArgumentException("Limite de data inválido no fuso da academia.");
        return repository.GetAsync(gymId, kind, query,
            new(today, zone, TimeZoneInfo.ConvertTimeToUtc(from, zone), TimeZoneInfo.ConvertTimeToUtc(until, zone), includeEnrollmentMetadata),
            export, cancellationToken);
    }

    public async Task<byte[]> ExportAsync(Guid gymId, string kind, ReportQuery query, CancellationToken token,
        bool includeEnrollmentMetadata = true)
    {
        var result = await GetAsync(gymId, kind, query, true, token, includeEnrollmentMetadata);
        return EncodeCsv(result);
    }

    public static byte[] EncodeCsv(ReportResult result)
    {
        // Quote every cell and neutralize formulas, including leading whitespace/control characters.
        static string Cell(string value)
        {
            var first = value.TrimStart();
            if (first.Length > 0 && "=+-@".Contains(first[0]) || value.StartsWith('\t') || value.StartsWith('\r'))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', result.Columns.Select(Cell)));
        foreach (var row in result.Rows) csv.AppendLine(string.Join(';', row.Select(Cell)));
        var encoding = new UTF8Encoding(true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(csv.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"))];
    }
}
