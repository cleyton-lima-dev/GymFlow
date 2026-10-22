using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

// Uses the existing isolated factory and the production JWT middleware, policies and repositories.
public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public Task ErpEndpoints_RequireExplicitTenantEntitlement_AndLegacyOperationsRemainAvailable() =>
        CheckErpEndpointEntitlement(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_ErpEndpoints_RequireExplicitTenantEntitlement_AndLegacyOperationsRemainAvailable(bool postgres) =>
        CheckErpEndpointEntitlement(postgres);

    private static async Task CheckErpEndpointEntitlement(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var disabledAdmin = factory.AddUser(factory.DisabledGym, UserRole.Admin);
        var legacyAdmin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var enabledAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();

        foreach (var (admin, expectedEnabled) in new[]
        {
            (disabledAdmin, false),
            (legacyAdmin, false),
            (enabledAdmin, true)
        })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
            var features = await client.GetFromJsonAsync<JsonElement>("/api/gym-features");
            Assert.Equal(expectedEnabled, features.GetProperty("erpEnabled").GetBoolean());

            var migrationResponse = await client.GetAsync("/api/migrations");
            Assert.Equal(expectedEnabled ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                migrationResponse.StatusCode);

            var importResponse = await client.PostAsJsonAsync("/api/students/imports/preview", new { rows = Array.Empty<object>() });
            if (expectedEnabled)
                Assert.NotEqual(HttpStatusCode.Forbidden, importResponse.StatusCode);
            else
                Assert.Equal(HttpStatusCode.Forbidden, importResponse.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(legacyAdmin));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/operational")).StatusCode);

        var legacyStudent = factory.AddUser(factory.LegacyGym);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            legacyStudent.Email,
            Password = TestApi.Password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var studentToken = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task IntegratedEndpoints_WithoutJwt_Return401()
    {
        await using var factory = new TestApi();
        using var client = factory.CreateClient();
        foreach (var endpoint in new[] { "/api/check-ins", "/api/physical-access/events", "/api/dashboard" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(endpoint)).StatusCode);
        foreach (var endpoint in new[] { "/api/check-ins/manual", "/api/access-agent/check-ins/physical-passage" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(endpoint, new { })).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Professor)]
    public async Task IntegratedEndpoints_NonAdminJwt_Return403(UserRole role)
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.LegacyGym, role);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        foreach (var endpoint in new[] { "/api/check-ins", "/api/physical-access/events", "/api/dashboard" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(endpoint)).StatusCode);
        foreach (var endpoint in new[] { "/api/check-ins/manual", "/api/access-agent/check-ins/physical-passage" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(endpoint, new { })).StatusCode);
    }

    [Fact]
    public Task IntegratedAdminEndpoints_IsolateTenantsAndDeduplicateCheckIns() => CheckIntegratedAdmin(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_IntegratedAdminEndpoints_IsolateTenantsAndDeduplicateCheckIns(bool postgres) =>
        CheckIntegratedAdmin(postgres);

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentManualRetries_RecordOneCheckIn(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var student = factory.AddUser(factory.LegacyGym);
        Guid studentId = default;
        factory.WithDb(db => studentId = db.Students.Single(s => s.UserId == student.Id).Id);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var request = new { studentId, requestId = Guid.NewGuid() };
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/check-ins/manual", request)));
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            }
        }
        Assert.Single(ids);
        factory.WithDb(db =>
        {
            var checkIn = Assert.Single(db.CheckIns);
            Assert.Equal(request.requestId, checkIn.RequestId);
            Assert.Equal(factory.LegacyGym, checkIn.GymId);
            Assert.Equal(studentId, checkIn.StudentId);
        });
    }

    private static async Task CheckIntegratedAdmin(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var student = factory.AddUser(factory.LegacyGym);
        var foreign = factory.AddUser(factory.EnabledGym);
        Guid studentId = default, foreignId = default;
        factory.WithDb(db =>
        {
            studentId = db.Students.Single(s => s.UserId == student.Id).Id;
            foreignId = db.Students.Single(s => s.UserId == foreign.Id).Id;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var request = new { studentId, requestId = Guid.NewGuid() };
        var first = await client.PostAsJsonAsync("/api/check-ins/manual", request);
        var repeat = await client.PostAsJsonAsync("/api/check-ins/manual", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/check-ins/manual",
            new { studentId = foreignId, request.requestId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/check-ins/manual",
            new { studentId = foreignId, requestId = Guid.NewGuid() })).StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>("/api/check-ins");
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
        Assert.Equal(studentId, history.GetProperty("items")[0].GetProperty("studentId").GetGuid());
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/operational");
        Assert.Equal(1, dashboard.GetProperty("activeStudents").GetInt32());
        Assert.False(dashboard.TryGetProperty("activeEnrollments", out _));
        Assert.False(dashboard.TryGetProperty("receivedRevenue", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/access-agent/check-ins/physical-passage", new { })).StatusCode);

        var otherAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/check-ins")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public Task IntegratedAccessAgent_AllowedIsNotPresenceAndPassageIsTenantScoped() => CheckIntegratedAgent(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_IntegratedAccessAgent_AllowedIsNotPresenceAndPassageIsTenantScoped(bool postgres) =>
        CheckIntegratedAgent(postgres);

    private static async Task CheckIntegratedAgent(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var student = factory.AddUser(factory.LegacyGym);
        var foreign = factory.AddUser(factory.EnabledGym);
        var ownRequest = Guid.NewGuid();
        var foreignRequest = Guid.NewGuid();
        var deniedRequest = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic integrated agent",
            MachineName = "isolated-test", SecretHash = "unused", CredentialVersion = 1
        };
        factory.WithDb(db =>
        {
            db.AccessAgents.Add(agent);
            foreach (var (user, request, decision) in new[]
            {
                (student, ownRequest, PhysicalAccessDecision.Allowed),
                (foreign, foreignRequest, PhysicalAccessDecision.Allowed),
                (student, deniedRequest, PhysicalAccessDecision.Denied)
            })
                db.PhysicalAccessEvents.Add(new PhysicalAccessEvent
                {
                    Id = Guid.NewGuid(), GymId = user.GymId, RequestId = request,
                    StudentId = db.Students.Single(s => s.UserId == user.Id).Id,
                    Decision = decision, Reason = PhysicalAccessDecisionReason.Eligible, OccurredAt = now
                });
            Assert.Empty(db.CheckIns);
        });
        using var scope = factory.Services.CreateScope();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        foreach (var endpoint in new[] { "/api/check-ins", "/api/physical-access/events", "/api/dashboard" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/check-ins/manual", new { })).StatusCode);
        const string passage = "/api/access-agent/check-ins/physical-passage";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(passage,
            new { requestId = foreignRequest, occurredAt = now })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(passage,
            new { requestId = deniedRequest, occurredAt = now })).StatusCode);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(passage,
                new { requestId = ownRequest, occurredAt = now })).StatusCode);
        factory.WithDb(db =>
        {
            var checkIn = Assert.Single(db.CheckIns);
            Assert.Equal(factory.LegacyGym, checkIn.GymId);
            Assert.Equal(CheckInSource.PhysicalAccess, checkIn.Source);
            Assert.Equal(ownRequest, checkIn.RequestId);
        });

        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var ownEvents = await client.GetFromJsonAsync<JsonElement>("/api/physical-access/events");
        Assert.Equal(2, ownEvents.GetProperty("totalCount").GetInt32());
        Assert.All(ownEvents.GetProperty("items").EnumerateArray(), item =>
            Assert.NotEqual(foreignRequest, item.GetProperty("requestId").GetGuid()));
        var otherAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var foreignEvents = await client.GetFromJsonAsync<JsonElement>("/api/physical-access/events");
        Assert.Equal(1, foreignEvents.GetProperty("totalCount").GetInt32());
        Assert.Equal(foreignRequest, foreignEvents.GetProperty("items")[0].GetProperty("requestId").GetGuid());
    }
}
