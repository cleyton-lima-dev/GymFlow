using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GymFlow.Api.Controllers;
using GymFlow.Api.Authorization;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Infrastructure.Security;
using GymFlow.Application.DTOs.Dashboard;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GymFlow.IntegrationTests.Dashboard;

public sealed class DashboardTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly DateTime From = new(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Until = From.AddMonths(1);

    [Fact]
    public Task Aggregates_SourceAmountsDatesStatesAndBothTenantRelationships() => CheckAggregates(false);

    [DashboardPostgresFact]
    public Task PostgreSql_Aggregates_SourceAmountsDatesStatesAndBothTenantRelationships() => CheckAggregates(true);

    private static async Task CheckAggregates(bool postgres)
    {
        var counter = new QueryCounter();
        await using var database = await DashboardTestDatabase.CreateAsync(postgres, counter);
        var db = database.Context;
        var gym = Guid.NewGuid();
        var other = Guid.NewGuid();
        var student = AddStudent(db, gym);
        var missing = AddStudent(db, gym);
        var inactive = AddStudent(db, gym, false);
        var archived = AddStudent(db, gym);
        archived.ArchivedAt = DateTime.UtcNow;
        var otherStudent = AddStudent(db, other);
        AddCharge(db, AddEnrollment(db, otherStudent, other, Today.AddDays(5)), ChargeStatus.Pending, 7777, Today.AddDays(-1));
        Assess(db, student, Today.AddMonths(-3));
        Assess(db, student, Today.AddMonths(-2)); // count latest, once
        Assess(db, inactive, Today.AddMonths(-4));
        var paid = AddEnrollment(db, student, gym, Today.AddDays(15));
        AddCharge(db, paid, ChargeStatus.Paid, 100, Today, 20, 80, From);
        var overdue = AddEnrollment(db, missing, gym, Today.AddDays(16));
        AddCharge(db, overdue, ChargeStatus.Pending, 200, Today.AddDays(-1));
        var dueToday = AddEnrollment(db, student, gym, Today.AddDays(1));
        AddCharge(db, dueToday, ChargeStatus.Pending, 50, Today);
        var previous = AddEnrollment(db, student, gym, Today, EnrollmentStatus.Active);
        AddCharge(db, previous, ChargeStatus.Paid, 40, Today.AddMonths(-1), 0, 40, Until.AddMicroseconds(-1));
        var outside = AddEnrollment(db, student, gym, Today.AddDays(20));
        AddCharge(db, outside, ChargeStatus.Paid, 30, Today.AddMonths(-1), 0, 30, Until);
        var cancelled = AddEnrollment(db, student, gym, Today.AddDays(20), EnrollmentStatus.Cancelled);
        AddCharge(db, cancelled, ChargeStatus.Cancelled, 999, Today);
        AddEnrollment(db, student, gym, Today.AddDays(20), EnrollmentStatus.PendingPayment);
        var future = AddEnrollment(db, student, gym, Today.AddDays(25));
        future.StartDate = Today.AddDays(1);
        var invalidRelationship = AddEnrollment(db, student, other, Today.AddDays(2));
        AddCharge(db, invalidRelationship, ChargeStatus.Pending, 9000, Today.AddDays(-1));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        counter.Reads = 0;
        var result = await new DashboardRepository(db).GetAsync(gym, Today, From, Until, default);
        Assert.Equal(3, counter.Reads); // Fixed aggregates, independent of student count.
        Assert.Equal(330m, result.ExpectedRevenue);
        Assert.Equal(120m, result.ReceivedRevenue);
        Assert.Equal(1, result.OverdueCharges);
        Assert.Equal(200m, result.OverdueAmount);
        Assert.Equal(4, result.ActiveEnrollments);
        Assert.Equal(2, result.ExpiringEnrollments);
        Assert.Equal(2, result.ActiveStudents);
        Assert.Equal(1, result.StudentsWithoutAssessment);
        Assert.Equal(1, result.ReassessmentsDue);
        Assert.Empty(db.ChangeTracker.Entries());
        var otherResult = await new DashboardRepository(db).GetAsync(other, Today, From, Until, default);
        Assert.Equal(7777m, otherResult.ExpectedRevenue);
        Assert.Equal(1, otherResult.ActiveStudents);
        Assert.Equal(1, otherResult.ActiveEnrollments);
        var empty = await new DashboardRepository(db).GetAsync(Guid.NewGuid(), Today, From, Until, default);
        Assert.Equal(0, empty.ExpectedRevenue);
        Assert.Equal(0, empty.ActiveStudents);
        Assert.Equal(0, empty.ActiveEnrollments);
    }

    [Theory]
    [InlineData(2027, 2, 28, 2026, 12, 31, true)]
    [InlineData(2026, 10, 30, 2026, 8, 31, false)]
    [InlineData(2026, 10, 31, 2026, 8, 31, true)]
    public Task AssessmentDeadline_MatchesExistingAddMonthsRule(int y, int m, int d, int ay, int am, int ad, bool due) =>
        CheckAssessment(false, y, m, d, ay, am, ad, due);

    [DashboardPostgresTheory]
    [InlineData(2027, 2, 28, 2026, 12, 31, true)]
    [InlineData(2026, 10, 30, 2026, 8, 31, false)]
    [InlineData(2026, 10, 31, 2026, 8, 31, true)]
    public Task PostgreSql_AssessmentDeadline_MatchesExistingAddMonthsRule(int y, int m, int d, int ay, int am, int ad, bool due) =>
        CheckAssessment(true, y, m, d, ay, am, ad, due);

    private static async Task CheckAssessment(bool postgres, int y, int m, int d, int ay, int am, int ad, bool due)
    {
        await using var database = await DashboardTestDatabase.CreateAsync(postgres);
        var db = database.Context;
        var gym = Guid.NewGuid();
        Assess(db, AddStudent(db, gym), new(ay, am, ad));
        await db.SaveChangesAsync();
        var result = await new DashboardRepository(db).GetAsync(gym, new(y, m, d), From, Until, default);
        Assert.Equal(due ? 1 : 0, result.ReassessmentsDue);
    }

    [Fact]
    public async Task LocalMonth_UsesGymTimezoneInsteadOfUtcDate()
    {
        var capture = new CaptureRepository();
        var service = new DashboardService(capture, new Zone(), new Clock());
        await service.GetAsync(Guid.NewGuid());
        Assert.Equal(new DateOnly(2026, 9, 30), capture.Today);
        Assert.Equal(new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc), capture.From);
        Assert.Equal(new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc), capture.Until);
    }

    [DashboardPostgresFact]
    public async Task PostgreSql_LocalMonthPaymentBoundariesDiscountsAndCancelledContracts()
    {
        await using var database = await DashboardTestDatabase.CreateAsync(true);
        var db = database.Context;
        var gym = Guid.NewGuid();
        var student = AddStudent(db, gym);
        var localToday = new DateOnly(2026, 9, 30); // Clock is October 1 UTC, September locally.
        var monthStart = new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        Enrollment Contract(ChargeStatus status, decimal amount, DateOnly due,
            decimal discount = 0, decimal? paid = null, DateTime? paidAt = null,
            EnrollmentStatus enrollmentStatus = EnrollmentStatus.Active)
        {
            var enrollment = AddEnrollment(db, student, gym, localToday.AddMonths(1), enrollmentStatus);
            AddCharge(db, enrollment, status, amount, due, discount, paid, paidAt);
            return enrollment;
        }

        var discounted = Contract(ChargeStatus.Paid, 100.15m, localToday, 20.10m, 80.05m, monthStart);
        Contract(ChargeStatus.Paid, 10, new(2026, 9, 1), 0, 10, monthStart.AddMicroseconds(-1));
        Contract(ChargeStatus.Paid, 30, localToday, 0, 30, monthEnd);
        Contract(ChargeStatus.Paid, 40, new(2026, 8, 31), 0, 40, monthEnd.AddMicroseconds(-1));
        Contract(ChargeStatus.Pending, 20, localToday); // Due today is not overdue.
        Contract(ChargeStatus.Pending, 23, localToday.AddDays(-1));
        Contract(ChargeStatus.Cancelled, 900, localToday, enrollmentStatus: EnrollmentStatus.Cancelled);
        Contract(ChargeStatus.Paid, 60, localToday, 0, 60, monthEnd.AddMicroseconds(-1), EnrollmentStatus.Cancelled);
        Contract(ChargeStatus.Pending, 50, new(2026, 10, 1)); // Exclusive forecast boundary.
        Contract(ChargeStatus.Pending, 11, new(2026, 8, 31));
        Contract(ChargeStatus.Overdue, 12, new(2026, 8, 30));
        // Audit cardinality must never multiply the underlying charge in aggregates.
        for (var i = 0; i < 2; i++)
            db.FinancialAuditLogs.Add(new FinancialAuditLog
            {
                Id = Guid.NewGuid(), GymId = gym, ActorUserId = student.User.Id,
                EntityType = nameof(Charge), EntityId = discounted.Charge!.Id,
                Action = FinancialAuditAction.PaymentConfirmed,
                PreviousValues = "{}", NewValues = "{}", OccurredAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new DashboardService(new DashboardRepository(db), new Zone(), new Clock()).GetAsync(gym);
        Assert.Equal(localToday, result.Today);
        Assert.Equal(new DateOnly(2026, 9, 1), result.PeriodStart);
        Assert.Equal(new DateOnly(2026, 10, 1), result.PeriodEndExclusive);
        Assert.Equal(223.05m, result.ExpectedRevenue);
        Assert.Equal(180.05m, result.ReceivedRevenue);
        Assert.Equal(3, result.OverdueCharges);
        Assert.Equal(46m, result.OverdueAmount);

        // Independently reconcile against the source module's scoped charge DTO data.
        // Its current-date statuses are not used for this historical deterministic clock.
        var financial = await new ChargeService(new ChargeRepository(db), new Zone(),
            new FinancialAuditRepository(db)).ListAsync(gym);
        Assert.Equal(result.ExpectedRevenue, financial.Where(c => c.Status != ChargeStatus.Cancelled &&
            c.DueDate >= result.PeriodStart && c.DueDate < result.PeriodEndExclusive)
            .Sum(c => c.Amount - c.DiscountAmount));
        Assert.Equal(result.ReceivedRevenue, financial.Where(c => c.Status == ChargeStatus.Paid &&
            c.PaidAt >= monthStart && c.PaidAt < monthEnd).Sum(c => c.PaidAmount ?? 0));
        Assert.Equal(2, await db.FinancialAuditLogs.CountAsync());
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Student", 403)]
    [InlineData("Professor", 403)]
    [InlineData("AccessAgent", 403)]
    [InlineData("Admin", 200)]
    [InlineData("AdminMissingGym", 401)]
    [InlineData("AdminInvalidSignature", 401)]
    public async Task HttpEndpoint_RequiresSignedAdminAndUsesAuthenticatedGym(string? role, int status)
    {
        const string key = "synthetic-dashboard-test-signing-key-only-123456789";
        var gym = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var capture = new CaptureRepository();
        builder.Services.AddSingleton<IDashboardRepository>(capture);
        builder.Services.AddSingleton<IGymTimeZoneProvider>(new Zone());
        builder.Services.AddSingleton<TimeProvider>(new Clock());
        builder.Services.AddScoped<DashboardService>();
        builder.Services.AddScoped<ErpEnabledFilter>();
        builder.Services.AddSingleton<IGymErpPolicy>(new ConfigurationGymErpPolicy(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"GymFeatures:{gym:D}:ErpEnabled"] = "true"
            }).Build()));
        builder.Services.AddControllers().AddApplicationPart(typeof(DashboardController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "dashboard-tests", ValidateAudience = true,
                ValidAudience = "dashboard-tests", ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateLifetime = true
            });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        if (role != null)
        {
            var tokenRole = role.StartsWith("Admin") ? "Admin" : role;
            var claims = new List<Claim> { new(ClaimTypes.Role, tokenRole) };
            if (role != "AdminMissingGym") claims.Add(new("gym_id", gym.ToString()));
            var signingKey = role == "AdminInvalidSignature" ? key + "invalid" : key;
            var token = new JwtSecurityToken("dashboard-tests", "dashboard-tests", claims,
                expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        var response = await client.GetAsync($"/api/dashboard?gymId={Guid.NewGuid()}");
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal(role == "Admin" ? gym : Guid.Empty, capture.Gym);
        if (role == "Admin") Assert.NotNull(await response.Content.ReadFromJsonAsync<DashboardSummary>());
        await app.StopAsync();
    }

    private static Student AddStudent(AppDbContext db, Guid gym, bool active = true)
    {
        var student = new Student { Id = Guid.NewGuid(), GymId = gym, Name = "Synthetic",
            IsActive = active, AppAccessEnabled = true,
            User = new User { Id = Guid.NewGuid(), GymId = gym, Name = "Synthetic", Email = $"{Guid.NewGuid()}@example.test", PasswordHash = "not-a-password", Role = UserRole.Student, IsActive = active } };
        db.Students.Add(student);
        return student;
    }
    private static Enrollment AddEnrollment(AppDbContext db, Student student, Guid gym, DateOnly end, EnrollmentStatus status = EnrollmentStatus.Active)
    {
        var enrollment = new Enrollment { Id = Guid.NewGuid(), Student = student,
            Plan = new Plan { Id = Guid.NewGuid(), GymId = gym, Name = Guid.NewGuid().ToString(), Price = 100, DurationMonths = 1, IsActive = true },
            PlanName = "Synthetic", PlanPrice = 100, PlanDurationMonths = 1,
            StartDate = Today.AddDays(-20), EndDate = end, Status = status };
        db.Enrollments.Add(enrollment);
        return enrollment;
    }
    private static void AddCharge(AppDbContext db, Enrollment enrollment, ChargeStatus status, decimal amount, DateOnly due,
        decimal discount = 0, decimal? paid = null, DateTime? paidAt = null) =>
        db.Charges.Add(new Charge { Id = Guid.NewGuid(), Enrollment = enrollment, Amount = amount, DueDate = due,
            Status = status, DiscountAmount = discount, PaidAmount = paid, PaidAt = paidAt });
    private static void Assess(AppDbContext db, Student student, DateOnly date) =>
        db.PhysicalAssessments.Add(new PhysicalAssessment { Id = Guid.NewGuid(), Student = student, AssessmentDate = date, WeightKg = 70, HeightCm = 170 });
    private sealed class Zone : IGymTimeZoneProvider
    {
        public TimeZoneInfo GetTimeZone(Guid gymId) => TimeZoneInfo.CreateCustomTimeZone("Synthetic -03", TimeSpan.FromHours(-3), "Synthetic -03", "Synthetic -03");
    }
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 1, 0, 0, TimeSpan.Zero);
    }
    private sealed class CaptureRepository : IDashboardRepository
    {
        public Guid Gym; public DateOnly Today; public DateTime From, Until;
        public Task<OperationalDashboardSummary> GetOperationalAsync(Guid gymId, DateOnly today, CancellationToken cancellationToken)
        {
            Gym = gymId; Today = today;
            return Task.FromResult(new OperationalDashboardSummary(today, 0, 0, 0));
        }
        public Task<DashboardSummary> GetAsync(Guid gymId, DateOnly today, DateTime paidFromUtc, DateTime paidUntilUtc, CancellationToken cancellationToken)
        {
            Gym = gymId; Today = today; From = paidFromUtc; Until = paidUntilUtc;
            return Task.FromResult(new DashboardSummary(today, today, today, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Reads;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
