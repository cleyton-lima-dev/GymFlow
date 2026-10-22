using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Theory]
    [InlineData("disabled")]
    [InlineData("missing")]
    public Task G6Mixed_AppOnly_DeniesFinancialSubresourcesAndKeepsOperationalReads(string mode) =>
        CheckMixedAppOnly(mode, false);

    [PostgresTheory]
    [InlineData("disabled")]
    [InlineData("missing")]
    public Task PostgreSql_G6Mixed_AppOnly_DeniesFinancialSubresourcesAndKeepsOperationalReads(string mode) =>
        CheckMixedAppOnly(mode, true);

    private static async Task CheckMixedAppOnly(string mode, bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var gym = mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        var student = factory.AddUser(gym);
        var enrollmentId = factory.AddEnrollment(student, gym, EnrollmentStatus.Active, -1, 20);
        Guid studentId = default;
        var chargeId = Guid.NewGuid();
        factory.WithDb(db =>
        {
            studentId = db.Students.Single(s => s.UserId == student.Id).Id;
            db.Charges.Add(new Charge
            {
                Id = chargeId, EnrollmentId = enrollmentId, Amount = 345,
                Status = ChargeStatus.Pending, DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        foreach (var path in FinancialReadPaths(studentId))
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/students/{studentId}/reactivate", new { planId = Guid.NewGuid(), startDate = "2026-10-01" })).StatusCode);

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/operational");
        Assert.Equal(4, dashboard.EnumerateObject().Count());
        Assert.Equal(1, dashboard.GetProperty("activeStudents").GetInt32());
        Assert.False(dashboard.TryGetProperty("receivedRevenue", out _));
        foreach (var kind in new[] { "students", "attendance", "access-events" })
            foreach (var suffix in new[] { "", "/export" })
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/reports/{kind}{suffix}")).StatusCode);
        var registry = await client.GetFromJsonAsync<JsonElement>("/api/students");
        var item = Assert.Single(registry.GetProperty("items").EnumerateArray());
        Assert.Equal(studentId, item.GetProperty("id").GetGuid());
        foreach (var field in new[] { "enrollmentStatus", "planName", "enrollmentEndDate" })
            Assert.Equal(JsonValueKind.Null, item.GetProperty(field).ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/students?enrollmentFilter=Active")).StatusCode);
        var registryReport = await client.GetFromJsonAsync<JsonElement>("/api/reports/students");
        Assert.Equal(new[] { "ID aluno", "Aluno", "Cadastro" },
            registryReport.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(1, registryReport.GetProperty("totalCount").GetInt32());
        var csv = await client.GetStringAsync("/api/reports/students/export");
        Assert.Contains(studentId.ToString(), csv);
        Assert.DoesNotContain("contrato", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrícula", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("345", csv);
        foreach (var suffix in new[] { "", "/export" })
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync($"/api/reports/students{suffix}?enrollmentStatus=valid")).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(ChargeStatus.Pending, db.Charges.Single(c => c.Id == chargeId).Status);
            Assert.Null(db.Charges.Single(c => c.Id == chargeId).PaidAt);
            Assert.Empty(db.FinancialAuditLogs);
            Assert.Null(db.Students.Single(s => s.Id == studentId).ArchivedAt);
            Assert.Equal(student.Id, db.Students.Single(s => s.Id == studentId).UserId);
        });
    }

    [Fact]
    public async Task G6Mixed_EntitlementFailure_Returns500WithoutGrantingErp_AndSharedReadsRemainAvailable()
    {
        await using var factory = new TestApi { ErpPolicyOverride = new ThrowingG6ErpPolicy() };
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var student = factory.AddUser(factory.LegacyGym);
        factory.AddEnrollment(student, factory.LegacyGym, EnrollmentStatus.Active, -1, 20);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        foreach (var path in FinancialReadPaths(Guid.NewGuid()).Concat(new[]
        {
            "/api/gym-features", "/api/plans", "/api/enrollments", "/api/charges", "/api/migrations"
        }))
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("Synthetic entitlement resolution failure", await response.Content.ReadAsStringAsync());
        }
        foreach (var path in new[] { "/api/dashboard/operational", "/api/students", "/api/reports/students", "/api/reports/attendance" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        var registry = await client.GetFromJsonAsync<JsonElement>("/api/students");
        Assert.Equal(JsonValueKind.Null, Assert.Single(registry.GetProperty("items").EnumerateArray()).GetProperty("planName").ValueKind);
        var report = await client.GetFromJsonAsync<JsonElement>("/api/reports/students");
        Assert.Equal(3, report.GetProperty("columns").GetArrayLength());
        factory.WithDb(db =>
        {
            Assert.Single(db.Plans);
            Assert.Single(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.True(db.Users.Single(u => u.Id == student.Id).IsActive);
        });
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("Professor", 403)]
    [InlineData("Student", 403)]
    public async Task G6Mixed_FinancialSurfaces_EnforceAuthenticationAndRoleBeforeEntitlement(string identity, int status)
    {
        await using var factory = new TestApi();
        factory.WithDb(_ => { });
        using var client = factory.CreateClient();
        if (identity != "anonymous")
        {
            var user = factory.AddUser(factory.LegacyGym, Enum.Parse<UserRole>(identity));
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        }
        foreach (var path in FinancialReadPaths(Guid.NewGuid()).Append("/api/gym-features"))
            Assert.Equal((HttpStatusCode)status, (await client.GetAsync(path)).StatusCode);
        Assert.Equal((HttpStatusCode)status, (await client.PostAsJsonAsync(
            $"/api/students/{Guid.NewGuid()}/reactivate", new { planId = Guid.NewGuid(), startDate = "2026-10-01" })).StatusCode);
    }

    [Fact]
    public async Task G6Mixed_EntitlementRevocation_DeniesNextRequestWithoutChangingJwt_AndPreservesSharedAccess()
    {
        var policy = new MutableG6ErpPolicy();
        await using var factory = new TestApi { ErpPolicyOverride = policy };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/plans")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard")).StatusCode);

        policy.Enabled = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/plans")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/financial/export")).StatusCode);
        var features = await client.GetFromJsonAsync<JsonElement>("/api/gym-features");
        Assert.False(features.GetProperty("erpEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/operational")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task G6Mixed_ProfessorInEntitledGym_KeepsRegistryButCannotReadEnrollmentMetadataOrFilterIt()
    {
        await using var factory = new TestApi();
        var professor = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        var student = factory.AddUser(factory.EnabledGym);
        factory.AddEnrollment(student, factory.EnabledGym, EnrollmentStatus.Active, -1, 20);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(professor));
        var registry = await client.GetFromJsonAsync<JsonElement>("/api/students");
        var item = Assert.Single(registry.GetProperty("items").EnumerateArray());
        foreach (var field in new[] { "enrollmentStatus", "planName", "enrollmentEndDate" })
            Assert.Equal(JsonValueKind.Null, item.GetProperty(field).ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/students?enrollmentFilter=None")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/students/{item.GetProperty("id").GetGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("empty")]
    [InlineData("unknown-tenant")]
    [InlineData("unknown-user")]
    public async Task G6Mixed_InvalidOrUnownedIdentity_Returns401(string scenario)
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.NameIdentifier, scenario == "unknown-user" ? Guid.NewGuid().ToString() : admin.Id.ToString())
        };
        var gymClaim = scenario switch
        {
            "missing" => null,
            "invalid" => "not-a-gym",
            "empty" => Guid.Empty.ToString(),
            "unknown-tenant" => Guid.NewGuid().ToString(),
            _ => factory.EnabledGym.ToString()
        };
        if (gymClaim is not null) claims.Add(new("gym_id", gymClaim));
        var token = new JwtSecurityToken("isolated-tests", "isolated-tests", claims,
            expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("synthetic-test-key-only-01234567890123456789")), SecurityAlgorithms.HmacSha256));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        foreach (var path in new[] { "/api/plans", "/api/gym-features", "/api/dashboard/operational" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public Task G6Mixed_TwoEntitledTenants_KeepReportsDashboardAndStudentFinanceIsolated() => CheckMixedTenantIsolation(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_G6Mixed_TwoEntitledTenants_KeepReportsDashboardAndStudentFinanceIsolated(bool postgres) =>
        CheckMixedTenantIsolation(postgres);

    private static async Task CheckMixedTenantIsolation(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var own = factory.AddUser(factory.EnabledGym);
        var foreign = factory.AddUser(factory.OtherEnabledGym);
        var ownEnrollment = factory.AddEnrollment(own, factory.EnabledGym, EnrollmentStatus.Active, -1, 20);
        var foreignEnrollment = factory.AddEnrollment(foreign, factory.OtherEnabledGym, EnrollmentStatus.Active, -1, 20);
        Guid ownId = default, foreignId = default;
        factory.WithDb(db =>
        {
            ownId = db.Students.Single(s => s.UserId == own.Id).Id;
            foreignId = db.Students.Single(s => s.UserId == foreign.Id).Id;
            foreach (var (enrollment, amount) in new[] { (ownEnrollment, 111m), (foreignEnrollment, 222m) })
                db.Charges.Add(new Charge
                {
                    Id = Guid.NewGuid(), EnrollmentId = enrollment, Amount = amount, PaidAmount = amount,
                    Status = ChargeStatus.Paid, PaymentOrigin = PaymentOrigin.Avelri,
                    PaidAt = DateTime.UtcNow, DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
                });
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var students = await client.GetFromJsonAsync<JsonElement>("/api/students?enrollmentFilter=Active");
        var ownRegistry = Assert.Single(students.GetProperty("items").EnumerateArray());
        Assert.Equal(ownId, ownRegistry.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.String, ownRegistry.GetProperty("planName").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, ownRegistry.GetProperty("enrollmentStatus").ValueKind);
        var cadastral = await client.GetFromJsonAsync<JsonElement>("/api/reports/students?enrollmentStatus=valid");
        Assert.Equal(7, cadastral.GetProperty("columns").GetArrayLength());
        Assert.Equal(1, cadastral.GetProperty("totalCount").GetInt32());
        var dashboard = await client.GetFromJsonAsync<JsonElement>($"/api/dashboard?gymId={factory.OtherEnabledGym}");
        Assert.Equal(111m, dashboard.GetProperty("receivedRevenue").GetDecimal());
        Assert.Equal(1, dashboard.GetProperty("activeStudents").GetInt32());
        var from = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var until = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40).ToString("yyyy-MM-dd");
        foreach (var kind in new[] { "financial", "enrollments" })
        {
            var report = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{kind}?fromDate={from}&toDate={until}&gymId={factory.OtherEnabledGym}");
            Assert.Equal(1, report.GetProperty("totalCount").GetInt32());
            var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{kind}?fromDate={from}&toDate={until}&studentId={foreignId}");
            Assert.Equal(0, filtered.GetProperty("totalCount").GetInt32());
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/students/{ownId}/financial-history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/students/{foreignId}/financial-history")).StatusCode);
    }

    private static string[] FinancialReadPaths(Guid studentId) =>
    [
        "/api/dashboard", "/api/reports/financial", "/api/reports/financial/export",
        "/api/reports/enrollments", "/api/reports/enrollments/export", $"/api/students/{studentId}/financial-history"
    ];

    private sealed class ThrowingG6ErpPolicy : IGymErpPolicy
    {
        public bool IsEnabled(Guid gymId) => throw new InvalidOperationException("Synthetic entitlement resolution failure");
    }

    private sealed class MutableG6ErpPolicy : IGymErpPolicy
    {
        public bool Enabled { get; set; } = true;
        public bool IsEnabled(Guid gymId) => Enabled;
    }
}
