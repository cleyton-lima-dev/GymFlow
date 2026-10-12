using System.Text;
using GymFlow.Application.DTOs.Reports;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using GymFlow.IntegrationTests.Dashboard;

namespace GymFlow.IntegrationTests.Reports;

public sealed class ReportTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly DateTime From = new(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Until = From.AddMonths(1);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Synthetic -03", TimeSpan.FromHours(-3), "Synthetic", "Synthetic");

    [Fact]
    public Task AllReports_IsolateRelationshipsFinancialDatesAndRecordedPresence() => VerifyReports(false);

    [DashboardPostgresFact]
    public Task PostgreSql_AllReports_IsolateRelationshipsFinancialDatesAndRecordedPresence() => VerifyReports(true);

    private static async Task VerifyReports(bool postgres)
    {
        await using var database = await DashboardTestDatabase.CreateAsync(postgres);
        var db = database.Context;
        var gym = Guid.NewGuid(); var other = Guid.NewGuid();
        var student = Student(db, gym, "Aluno sintético");
        var foreign = Student(db, other, "Não exportar outro tenant");
        Charge(db, Enrollment(db, student, gym), 100, ChargeStatus.Paid, Today, 20, 80, From);
        Charge(db, Enrollment(db, student, gym), 50, ChargeStatus.Pending, Today.AddDays(-1));
        Charge(db, Enrollment(db, student, gym), 400, ChargeStatus.Cancelled, Today);
        Charge(db, Enrollment(db, student, gym), 30, ChargeStatus.Paid, new(2026, 9, 30), 0, 30, Until.AddMicroseconds(-1));
        Charge(db, Enrollment(db, student, gym), 95, ChargeStatus.Paid, new(2026, 9, 30), 0, 95, Until);
        Charge(db, Enrollment(db, student, other), 9999, ChargeStatus.Paid, Today, 0, 9999, From);
        Charge(db, Enrollment(db, foreign, gym), 8888, ChargeStatus.Paid, Today, 0, 8888, From);
        var pending = Enrollment(db, student, gym); pending.Status = EnrollmentStatus.PendingPayment;
        var future = Enrollment(db, student, gym); future.StartDate = Today.AddDays(1);
        var expired = Enrollment(db, student, gym); expired.EndDate = Today;
        var cancelled = Enrollment(db, student, gym); cancelled.Status = EnrollmentStatus.Cancelled;
        var allowed = Access(db, gym, student, From, PhysicalAccessDecision.Allowed);
        Access(db, gym, student, From.AddHours(1), PhysicalAccessDecision.Allowed);
        Access(db, gym, student, Until, PhysicalAccessDecision.Allowed); // exclusive boundary
        Access(db, gym, foreign, From, PhysicalAccessDecision.Allowed); // inconsistent relationship
        Access(db, other, foreign, From, PhysicalAccessDecision.Allowed);
        CheckIn(db, gym, student, From, CheckInSource.Manual);
        var physical = CheckIn(db, gym, student, Until.AddSeconds(-1), CheckInSource.PhysicalAccess);
        physical.PhysicalAccessEventId = allowed.Id;
        CheckIn(db, gym, student, From.AddMicroseconds(-1), CheckInSource.Manual);
        CheckIn(db, gym, student, Until, CheckInSource.Manual);
        CheckIn(db, gym, foreign, From, CheckInSource.Manual);
        var wrongRecorder = CheckIn(db, gym, student, From, CheckInSource.Manual);
        wrongRecorder.RecordedByUserId = foreign.UserId;
        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(), GymId = gym, StudentId = student.Id,
            ExternalIdentifier = "sensitive-credential-never-export", ProviderKey = "provider-secret-not-exported", Type = PhysicalAccessCredentialType.Card
        };
        db.PhysicalAccessCredentials.Add(credential);
        allowed.CredentialId = credential.Id;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repo = new ReportRepository(db);
        var context = new ReportContext(Today, Zone, From, Until);
        ReportQuery Query(string view = "charges") => new() { FromDate = new(2026, 10, 1), ToDate = new(2026, 10, 31), View = view };
        var charges = await repo.GetAsync(gym, "financial", Query(), context, false, default);
        Assert.Equal(3, charges.TotalCount);
        Assert.Equal("130,00", charges.Totals["Líquido não cancelado (R$)"]);
        Assert.Equal("80,00", charges.Totals["Recebido das cobranças selecionadas (R$)"]);
        var revenue = await repo.GetAsync(gym, "financial", Query("revenue"), context, false, default);
        Assert.Equal(2, revenue.TotalCount);
        Assert.Equal("110,00", revenue.Totals["Recebido das cobranças selecionadas (R$)"]);
        Assert.Contains(revenue.Rows, r => r[9] == "01/10/2026 00:00:00");
        var overdue = await repo.GetAsync(gym, "financial", Query("overdue"), context, false, default);
        Assert.Equal(1, overdue.TotalCount);
        Assert.Equal("50,00", overdue.Totals["Vencido (R$)"]);
        Assert.Equal("Vencida", overdue.Rows[0][8]);
        var attendance = await repo.GetAsync(gym, "attendance", Query(), context, false, default);
        Assert.Equal(2, attendance.TotalCount);
        Assert.Equal("1", attendance.Totals["Alunos distintos"]);
        var events = await repo.GetAsync(gym, "access-events", Query(), context, false, default);
        Assert.Equal(2, events.TotalCount);
        Assert.Equal(1, events.Rows.Count(r => r[6] == "Sim"));
        Assert.Equal(1, events.Rows.Count(r => r[6] == "Não")); // Allowed alone is never presence.
        var students = await repo.GetAsync(gym, "students", new() { Status = "active", EnrollmentStatus = "valid" }, context, false, default);
        Assert.Equal(1, students.TotalCount);
        foreach (var (status, expected) in new[] { ("active", 5), ("future", 1), ("expired", 1), ("pending-payment", 1), ("cancelled", 1) })
        {
            var filter = Query(); filter.Status = status;
            Assert.Equal(expected, (await repo.GetAsync(gym, "enrollments", filter, context, false, default)).TotalCount);
        }
        foreach (var kind in new[] { "financial", "enrollments", "attendance", "access-events" })
        {
            var filter = Query(); filter.StudentId = foreign.Id;
            Assert.Empty((await repo.GetAsync(gym, kind, filter, context, true, default)).Rows);
            filter.StudentId = student.Id; filter.Search = "SINTÉTICO";
            var all = await repo.GetAsync(gym, kind, filter, context, true, default);
            filter.PageSize = 1;
            var page = await repo.GetAsync(gym, kind, filter, context, false, default);
            Assert.Equal(all.TotalCount, page.TotalCount);
            Assert.Equal(all.Rows[0], page.Rows[0]);
            var csv = Encoding.UTF8.GetString(ReportService.EncodeCsv(all));
            Assert.DoesNotContain("sensitive-credential", csv);
            Assert.DoesNotContain("provider-secret", csv);
            Assert.DoesNotContain(foreign.User.Name, csv);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExportOverLimit_IsRejectedRatherThanTruncated()
    {
        await using var database = await DashboardTestDatabase.CreateAsync(false);
        var gym = Guid.NewGuid();
        for (var i = 0; i <= ReportService.ExportLimit; i++) Student(database.Context, gym, "Synthetic");
        await database.Context.SaveChangesAsync();
        var repo = new ReportRepository(database.Context);
        var c = new ReportContext(Today, Zone, From, Until);
        Assert.Single((await repo.GetAsync(gym, "students", new() { PageSize = 1 }, c, false, default)).Rows);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.GetAsync(gym, "students", new(), c, true, default));
    }

    [Theory]
    [InlineData("unknown", null, null, null)]
    [InlineData("financial", "invalid", null, null)]
    [InlineData("financial", null, "2026-10-12", "2026-10-01")]
    [InlineData("financial", null, "2024-01-01", "2026-01-01")]
    [InlineData("financial", null, "2026-10-01", null)]
    [InlineData("financial", null, "9999-12-01", "9999-12-31")]
    [InlineData("students", null, "2026-10-01", "2026-10-31")]
    public async Task InvalidFilters_AreRejected(string kind, string? status, string? from, string? until)
    {
        await using var db = await DashboardTestDatabase.CreateAsync(false);
        var service = new ReportService(new ReportRepository(db.Context), new Zones(), new Clock());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(Guid.NewGuid(), kind, new()
        { Status = status, FromDate = from == null ? null : DateOnly.Parse(from), ToDate = until == null ? null : DateOnly.Parse(until) }));
    }

    [Fact]
    public async Task DefaultPeriod_UsesGymMonthNotUtcMonth()
    {
        await using var db = await DashboardTestDatabase.CreateAsync(false);
        var service = new ReportService(new ReportRepository(db.Context), new Zones(), new Clock());
        var result = await service.GetAsync(Guid.NewGuid(), "financial", new());
        Assert.Equal(new DateOnly(2026, 9, 1), result.FromDate);
        Assert.Equal(new DateOnly(2026, 9, 30), result.ToDate);
    }

    [Fact]
    public void Csv_HasBomBrazilianNumbersQuotesAndFormulaProtection()
    {
        var result = new ReportResult("", "", "", null, null, ["Aluno", "Valor"],
            [[" =HYPERLINK(\"bad\");ação\nlinha", "80,25"], ["@SUM(1)", "0,00"]], 2, 1, 25, new());
        var bytes = ReportService.EncodeCsv(result);
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, bytes.Take(3).ToArray());
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"' =HYPERLINK(\"\"bad\"\");ação\r\nlinha\";\"80,25\"", csv);
        Assert.Contains("\"'@SUM(1)\"", csv);
        Assert.EndsWith("\r\n", csv);
    }

    private static Student Student(AppDbContext db, Guid gym, string name)
    {
        var user = new User { Id = Guid.NewGuid(), GymId = gym, Name = name, Email = $"{Guid.NewGuid()}@example.invalid", PasswordHash = "synthetic", Role = UserRole.Student };
        var student = new Student { Id = Guid.NewGuid(), User = user, UserId = user.Id };
        db.Students.Add(student); return student;
    }
    private static Enrollment Enrollment(AppDbContext db, Student student, Guid gym)
    {
        var item = new Enrollment { Id = Guid.NewGuid(), Student = student, StudentId = student.Id,
            Plan = new Plan { Id = Guid.NewGuid(), GymId = gym, Name = $"Plano sintético {Guid.NewGuid():N}", Price = 100, DurationMonths = 1 },
            PlanName = "Plano sintético", PlanPrice = 100, StartDate = Today.AddDays(-10), EndDate = Today.AddDays(10) };
        db.Enrollments.Add(item); return item;
    }
    private static void Charge(AppDbContext db, Enrollment enrollment, decimal amount, ChargeStatus status, DateOnly due,
        decimal discount = 0, decimal? paid = null, DateTime? paidAt = null) =>
        db.Charges.Add(new Charge { Id = Guid.NewGuid(), Enrollment = enrollment, Amount = amount, Status = status,
            DueDate = due, DiscountAmount = discount, PaidAmount = paid, PaidAt = paidAt });
    private static CheckIn CheckIn(AppDbContext db, Guid gym, Student student, DateTime at, CheckInSource source)
    {
        var item = new CheckIn { Id = Guid.NewGuid(), GymId = gym, StudentId = student.Id, RequestId = Guid.NewGuid(), OccurredAt = at, Source = source };
        db.CheckIns.Add(item); return item;
    }
    private static PhysicalAccessEvent Access(AppDbContext db, Guid gym, Student student, DateTime at, PhysicalAccessDecision decision)
    {
        var item = new PhysicalAccessEvent { Id = Guid.NewGuid(), GymId = gym, StudentId = student.Id, RequestId = Guid.NewGuid(), OccurredAt = at,
            Decision = decision, Reason = PhysicalAccessDecisionReason.Eligible };
        db.PhysicalAccessEvents.Add(item); return item;
    }
    private sealed class Zones : IGymTimeZoneProvider { public TimeZoneInfo GetTimeZone(Guid gymId) => Zone; }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 1, 0, 0, TimeSpan.Zero); }
}
