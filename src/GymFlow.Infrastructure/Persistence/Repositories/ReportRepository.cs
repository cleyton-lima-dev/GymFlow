using System.Globalization;
using GymFlow.Application.DTOs.Reports;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

// Read-only projections. Financial rows come from Charges once, never from audit or attendance joins.
public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    public Task<ReportResult> GetAsync(Guid gymId, string kind, ReportQuery query, ReportContext context,
        bool export, CancellationToken cancellationToken) => kind switch
    {
        "students" => Students(gymId, query, context, export, cancellationToken),
        "financial" => Financial(gymId, query, context, export, cancellationToken),
        "enrollments" => Enrollments(gymId, query, context, export, cancellationToken),
        "attendance" => Attendance(gymId, query, context, export, cancellationToken),
        "access-events" => Events(gymId, query, context, export, cancellationToken),
        _ => throw new ArgumentException("Relatório inválido.")
    };

    private IQueryable<Enrollment> TenantEnrollments(Guid gym) => db.Enrollments.AsNoTracking()
        .Where(e => e.Student.GymId == gym && e.Plan.GymId == gym);
    private static string Money(decimal value) => value.ToString("F2", ReportService.Brazilian);
    private static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
    private static string Instant(DateTime? value, ReportContext c) => value.HasValue
        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc), c.Zone)
            .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "";
    private static string EnrollmentLabel(EnrollmentStatus? status, DateOnly? start, DateOnly? end, DateOnly today) => status switch
    {
        EnrollmentStatus.Active when end <= today => "Expirada",
        EnrollmentStatus.Active when start > today => "Futura",
        EnrollmentStatus.Active => "Vigente",
        EnrollmentStatus.Cancelled => "Cancelada",
        EnrollmentStatus.Expired => "Expirada",
        EnrollmentStatus.PendingPayment => "Pagamento pendente",
        _ => "Sem matrícula"
    };
    private static string ChargeLabel(ChargeStatus status, DateOnly due, DateOnly today) => status switch
    {
        ChargeStatus.Pending when due < today => "Vencida",
        ChargeStatus.Pending => "Pendente",
        ChargeStatus.Overdue => "Vencida",
        ChargeStatus.Paid => "Paga",
        _ => "Cancelada"
    };
    private static IQueryable<T> Window<T>(IQueryable<T> ordered, ReportQuery q, bool export) => export
        ? ordered.Take(ReportService.ExportLimit + 1) : ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize);
    private static async Task<int> Count<T>(IQueryable<T> source, bool export, CancellationToken token)
    {
        var count = await source.CountAsync(token);
        if (export && count > ReportService.ExportLimit)
            throw new ArgumentException($"A exportação permite até {ReportService.ExportLimit} registros. Restrinja os filtros.");
        return count;
    }
    private static ReportResult Result(string title, string basis, ReportQuery q, ReportContext c,
        string[] columns, List<string[]> rows, int count, bool export, Dictionary<string, string>? totals = null)
    {
        if (export && rows.Count > ReportService.ExportLimit)
            throw new ArgumentException("O limite de exportação foi excedido. Restrinja os filtros.");
        return new(title, basis, c.Zone.Id, q.FromDate, q.ToDate, columns, rows, count,
            export ? 1 : q.Page, export ? ReportService.ExportLimit : q.PageSize,
            totals ?? new() { ["Registros"] = count.ToString(CultureInfo.InvariantCulture) });
    }

    private async Task<ReportResult> Students(Guid gym, ReportQuery q, ReportContext c, bool export, CancellationToken token)
    {
        var source = db.Students.AsNoTracking().Where(s => s.GymId == gym);
        if (q.StudentId.HasValue) source = source.Where(s => s.Id == q.StudentId);
        if (q.Search != null) { var search = q.Search.ToLower(); source = source.Where(s => s.Name.ToLower().Contains(search)); }
        source = q.Status switch
        {
            "active" => source.Where(s => s.IsActive && s.ArchivedAt == null),
            "inactive" => source.Where(s => !s.IsActive && s.ArchivedAt == null),
            "archived" => source.Where(s => s.ArchivedAt != null),
            _ => source
        };
        if (!c.IncludeEnrollmentMetadata)
        {
            var sharedCount = await Count(source, export, token);
            var sharedRows = await Window(source.OrderBy(s => s.Name).ThenBy(s => s.Id)
                .Select(s => new { s.Id, s.Name, s.IsActive, s.ArchivedAt }), q, export).ToListAsync(token);
            return Result("Alunos", "Situação cadastral atual.", q, c,
                ["ID aluno", "Aluno", "Cadastro"],
                sharedRows.Select(s => new[] { s.Id.ToString(), s.Name,
                    s.ArchivedAt != null ? "Arquivado" : s.IsActive ? "Ativo" : "Inativo" }).ToList(), sharedCount, export);
        }
        var enrollments = TenantEnrollments(gym);
        var projected = source.Select(s => new
        {
            s.Id, s.Name, s.IsActive, s.ArchivedAt, s.InactivationReason,
            Valid = enrollments.Any(e => e.StudentId == s.Id && e.Status == EnrollmentStatus.Active && e.StartDate <= c.Today && e.EndDate > c.Today),
            LastStatus = enrollments.Where(e => e.StudentId == s.Id).OrderByDescending(e => e.EndDate).ThenBy(e => e.Id)
                .Select(e => (EnrollmentStatus?)e.Status).FirstOrDefault(),
            LastStart = enrollments.Where(e => e.StudentId == s.Id).OrderByDescending(e => e.EndDate).ThenBy(e => e.Id)
                .Select(e => (DateOnly?)e.StartDate).FirstOrDefault(),
            LastEnd = enrollments.Where(e => e.StudentId == s.Id).OrderByDescending(e => e.EndDate).ThenBy(e => e.Id)
                .Select(e => (DateOnly?)e.EndDate).FirstOrDefault()
        });
        if (q.EnrollmentStatus != null) projected = projected.Where(s => s.Valid == (q.EnrollmentStatus == "valid"));
        var count = await Count(projected, export, token);
        var rows = await Window(projected.OrderBy(s => s.Name).ThenBy(s => s.Id), q, export).ToListAsync(token);
        return Result("Alunos", "Situação atual; matrícula vigente exige início ≤ hoje e fim > hoje. Não representa autorização física.", q, c,
            ["ID aluno", "Aluno", "Cadastro", "Motivo de inativação", "Matrícula vigente", "Último contrato", "Vencimento do último contrato"],
            rows.Select(s => new[] { s.Id.ToString(), s.Name, s.ArchivedAt != null ? "Arquivado" : s.IsActive ? "Ativo" : "Inativo",
                s.InactivationReason == StudentInactivationReason.Manual ? "Manual" : s.InactivationReason == null ? "" : "Ausência de matrícula",
                s.Valid ? "Sim" : "Não", EnrollmentLabel(s.LastStatus, s.LastStart, s.LastEnd, c.Today), Date(s.LastEnd) }).ToList(), count, export);
    }

    private async Task<ReportResult> Enrollments(Guid gym, ReportQuery q, ReportContext c, bool export, CancellationToken token)
    {
        var source = TenantEnrollments(gym).Where(e => e.EndDate >= q.FromDate && e.EndDate <= q.ToDate);
        if (q.StudentId.HasValue) source = source.Where(e => e.StudentId == q.StudentId);
        if (q.Search != null) { var search = q.Search.ToLower(); source = source.Where(e => e.Student.Name.ToLower().Contains(search)); }
        source = q.Status switch
        {
            "active" => source.Where(e => e.Status == EnrollmentStatus.Active && e.StartDate <= c.Today && e.EndDate > c.Today),
            "future" => source.Where(e => e.Status == EnrollmentStatus.Active && e.StartDate > c.Today && e.EndDate > c.Today),
            "expired" => source.Where(e => e.Status == EnrollmentStatus.Expired || e.Status == EnrollmentStatus.Active && e.EndDate <= c.Today),
            "pending-payment" => source.Where(e => e.Status == EnrollmentStatus.PendingPayment),
            "cancelled" => source.Where(e => e.Status == EnrollmentStatus.Cancelled),
            _ => source
        };
        var count = await Count(source, export, token);
        var rows = await Window(source.OrderBy(e => e.EndDate).ThenBy(e => e.Id).Select(e => new
        { e.Id, e.Student.Name, e.PlanName, e.PlanPrice, e.StartDate, e.EndDate, e.Status }), q, export).ToListAsync(token);
        return Result("Matrículas", "Período aplicado ao vencimento (fim exclusivo da vigência); situação calculada na data local de hoje.", q, c,
            ["ID matrícula", "Aluno", "Plano contratado", "Valor contratado (R$)", "Início", "Vencimento", "Situação"],
            rows.Select(e => new[] { e.Id.ToString(), e.Name, e.PlanName, Money(e.PlanPrice), Date(e.StartDate), Date(e.EndDate),
                EnrollmentLabel(e.Status, e.StartDate, e.EndDate, c.Today) }).ToList(), count, export);
    }

    private async Task<ReportResult> Financial(Guid gym, ReportQuery q, ReportContext c, bool export, CancellationToken token)
    {
        var source = db.Charges.AsNoTracking().Where(x => x.Enrollment.Student.GymId == gym && x.Enrollment.Plan.GymId == gym);
        var paidView = q.View is "payments" or "revenue";
        source = paidView
            ? source.Where(x => x.Status == ChargeStatus.Paid && x.PaidAt >= c.FromUtc && x.PaidAt < c.UntilUtc)
            : source.Where(x => x.DueDate >= q.FromDate && x.DueDate <= q.ToDate);
        if (q.View == "overdue") source = source.Where(x => x.Status == ChargeStatus.Overdue || x.Status == ChargeStatus.Pending && x.DueDate < c.Today);
        if (q.StudentId.HasValue) source = source.Where(x => x.Enrollment.StudentId == q.StudentId);
        if (q.Search != null) { var search = q.Search.ToLower(); source = source.Where(x => x.Enrollment.Student.Name.ToLower().Contains(search)); }
        source = q.Status switch
        {
            "pending" => source.Where(x => x.Status == ChargeStatus.Pending && x.DueDate >= c.Today),
            "overdue" => source.Where(x => x.Status == ChargeStatus.Overdue || x.Status == ChargeStatus.Pending && x.DueDate < c.Today),
            "paid" => source.Where(x => x.Status == ChargeStatus.Paid),
            "cancelled" => source.Where(x => x.Status == ChargeStatus.Cancelled),
            _ => source
        };
        var count = await Count(source, export, token);
        var totals = await source.GroupBy(_ => 1).Select(g => new
        {
            Net = g.Sum(x => x.Status != ChargeStatus.Cancelled ? x.Amount - x.DiscountAmount : 0m),
            Received = g.Sum(x => x.Status == ChargeStatus.Paid && x.PaymentOrigin == PaymentOrigin.Avelri ? x.PaidAmount ?? 0m : 0m),
            Overdue = g.Sum(x => x.Status == ChargeStatus.Overdue || x.Status == ChargeStatus.Pending && x.DueDate < c.Today ? x.Amount - x.DiscountAmount : 0m)
        }).SingleOrDefaultAsync(token);
        var ordered = paidView ? source.OrderBy(x => x.PaidAt).ThenBy(x => x.Id) : source.OrderBy(x => x.DueDate).ThenBy(x => x.Id);
        var rows = await Window(ordered.Select(x => new
        { x.Id, x.Enrollment.Student.Name, x.Enrollment.PlanName, x.Amount, x.DiscountAmount, x.PaidAmount, x.PaymentMethod, x.DueDate, x.Status, x.PaidAt }), q, export).ToListAsync(token);
        return Result(q.View switch { "payments" => "Pagamentos", "revenue" => "Receita recebida", "overdue" => "Inadimplência", _ => "Cobranças" },
            paidView ? "Período aplicado ao pagamento efetivo, no fuso da academia. Receita = PaidAmount de cobranças pagas."
                : "Período aplicado ao vencimento. Total líquido exclui canceladas; valores recebidos são das cobranças selecionadas, mesmo se pagos em outro período.", q, c,
            ["ID cobrança", "Aluno", "Plano", "Valor (R$)", "Desconto (R$)", "Líquido (R$)", "Pago (R$)", "Vencimento", "Situação", $"Pagamento local ({c.Zone.Id})", "Forma"],
            rows.Select(x => new[] { x.Id.ToString(), x.Name, x.PlanName, Money(x.Amount), Money(x.DiscountAmount), Money(x.Amount - x.DiscountAmount),
                x.PaidAmount.HasValue ? Money(x.PaidAmount.Value) : "", Date(x.DueDate), ChargeLabel(x.Status, x.DueDate, c.Today), Instant(x.PaidAt, c),
                x.PaymentMethod switch { PaymentMethod.Pix => "Pix", PaymentMethod.Card => "Cartão", PaymentMethod.Cash => "Dinheiro", PaymentMethod.Other => "Outro", _ => "" } }).ToList(), count, export,
            new() { ["Registros"] = count.ToString(), ["Líquido não cancelado (R$)"] = Money(totals?.Net ?? 0),
                ["Recebido das cobranças selecionadas (R$)"] = Money(totals?.Received ?? 0), ["Vencido (R$)"] = Money(totals?.Overdue ?? 0) });
    }

    private async Task<ReportResult> Attendance(Guid gym, ReportQuery q, ReportContext c, bool export, CancellationToken token)
    {
        var source = db.CheckIns.AsNoTracking().Where(x => x.GymId == gym && x.Student.GymId == gym &&
            (x.RecordedByUserId == null || db.Users.Any(u => u.Id == x.RecordedByUserId && u.GymId == gym)) &&
            (x.PhysicalAccessEventId == null || x.PhysicalAccessEvent!.GymId == gym && x.PhysicalAccessEvent.StudentId == x.StudentId) &&
            x.OccurredAt >= c.FromUtc && x.OccurredAt < c.UntilUtc);
        if (q.StudentId.HasValue) source = source.Where(x => x.StudentId == q.StudentId);
        if (q.Search != null) { var search = q.Search.ToLower(); source = source.Where(x => x.Student.Name.ToLower().Contains(search)); }
        if (q.Status == "manual") source = source.Where(x => x.Source == CheckInSource.Manual);
        if (q.Status == "physical") source = source.Where(x => x.Source == CheckInSource.PhysicalAccess && x.PhysicalAccessEventId != null);
        var count = await Count(source, export, token);
        var students = await source.Select(x => x.StudentId).Distinct().CountAsync(token);
        var rows = await Window(source.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => new
        { x.Id, x.Student.Name, x.Source, x.OccurredAt, x.RequestId, x.PhysicalAccessEventId }), q, export).ToListAsync(token);
        return Result("Frequência", "Check-ins efetivamente registrados. Check-in manual não comprova passagem física; autorização Allowed não é presença.", q, c,
            ["ID check-in", "Aluno", $"Data local ({c.Zone.Id})", "Origem", "Passagem confirmada registrada", "RequestId"],
            rows.Select(x => new[] { x.Id.ToString(), x.Name, Instant(x.OccurredAt, c), x.Source == CheckInSource.Manual ? "Manual" : "Acesso físico",
                x.Source == CheckInSource.PhysicalAccess && x.PhysicalAccessEventId != null ? "Sim (registro da API)" : "Não", x.RequestId.ToString() }).ToList(), count, export,
            new() { ["Check-ins"] = count.ToString(), ["Alunos distintos"] = students.ToString() });
    }

    private async Task<ReportResult> Events(Guid gym, ReportQuery q, ReportContext c, bool export, CancellationToken token)
    {
        var source = db.PhysicalAccessEvents.AsNoTracking().Where(x => x.GymId == gym &&
            (x.StudentId == null || x.Student!.GymId == gym) &&
            (x.CredentialId == null || x.Credential!.GymId == gym && x.Credential.Student.GymId == gym &&
                (x.StudentId == null || x.Credential.StudentId == x.StudentId)) && x.OccurredAt >= c.FromUtc && x.OccurredAt < c.UntilUtc);
        if (q.StudentId.HasValue) source = source.Where(x => x.StudentId == q.StudentId);
        if (q.Search != null) { var search = q.Search.ToLower(); source = source.Where(x => x.Student != null && x.Student.Name.ToLower().Contains(search)); }
        if (q.Status == "allowed") source = source.Where(x => x.Decision == PhysicalAccessDecision.Allowed);
        if (q.Status == "denied") source = source.Where(x => x.Decision == PhysicalAccessDecision.Denied);
        var count = await Count(source, export, token);
        var rows = await Window(source.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => new
        {
            x.Id, Name = x.Student != null ? x.Student.Name : "Não identificado", x.OccurredAt, x.Decision, x.Reason, x.Source, x.RequestId,
            Passage = db.CheckIns.Any(check => check.GymId == gym && check.Student.GymId == gym &&
                check.StudentId == x.StudentId && check.PhysicalAccessEventId == x.Id && check.Source == CheckInSource.PhysicalAccess)
        }), q, export).ToListAsync(token);
        return Result("Eventos de acesso", "Decisões e passagens são eventos distintos. Confirmação indica registro recebido pela API; não atesta homologação do equipamento.", q, c,
            ["ID evento", "Aluno", $"Data local ({c.Zone.Id})", "Decisão", "Motivo", "Origem", "Presença registrada por passagem", "RequestId"],
            rows.Select(x => new[] { x.Id.ToString(), x.Name, Instant(x.OccurredAt, c), x.Decision == PhysicalAccessDecision.Allowed ? "Autorizado" : "Negado",
                x.Reason.ToString(), x.Source.ToString(), x.Passage ? "Sim" : "Não", x.RequestId.ToString() }).ToList(), count, export);
    }
}
