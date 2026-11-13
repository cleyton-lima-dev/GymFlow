using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task Maintenance_BlocksOldTokensAndAnonymousTenantFlows_ThenRestoresAccess()
    {
        await using var factory = new TestApi { EnablePlatform = true };
        var authorized = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var otherAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var professor = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        var student = factory.AddUser(factory.EnabledGym);
        factory.AddEnrollment(student, factory.EnabledGym, EnrollmentStatus.Active, -1, 30);
        var tenantB = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        var actor = new PlatformOperator
        {
            Id = Guid.NewGuid(), Name = "Synthetic operator", Email = "operator@example.invalid",
            PasswordHash = "test-only", ProtectedTotpSecret = "test-only", IsActive = true,
            CredentialVersion = 1, CreatedAt = DateTime.UtcNow
        };
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Synthetic agent",
            MachineName = "synthetic-machine", SecretHash = "test-only", IsActive = true
        };
        var inviteToken = new string('A', 64);
        var adminInviteToken = new string('B', 64);
        factory.WithDb(db =>
        {
            db.PlatformOperators.Add(actor);
            db.AccessAgents.Add(agent);
            var invitee = new Student
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Synthetic invitee",
                Email = "invitee@example.invalid", IsActive = true, AppAccessEnabled = true,
                AppInviteTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inviteToken))),
                AppInviteExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            db.Students.Add(invitee);
            db.GymSettings.Add(new GymSettings
            {
                GymId = factory.EnabledGym, AdministrativeName = "Synthetic gym",
                TimeZoneId = "UTC", Version = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            db.GymOnboardings.Add(new GymOnboarding
            {
                GymId = factory.EnabledGym, OperatorId = actor.Id, AdminId = authorized.Id,
                IdempotencyKey = "synthetic-key", RequestHash = "synthetic-hash",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            db.AdminActivationInvitations.Add(new AdminActivationInvitation
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, AdminId = authorized.Id,
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(adminInviteToken))),
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
        });

        using var scope = factory.Services.CreateScope();
        agent.SecretHash = scope.ServiceProvider.GetRequiredService<IAccessAgentSecretService>()
            .Hash("synthetic-agent-secret");
        factory.WithDb(db => db.AccessAgents.Single(x => x.Id == agent.Id).SecretHash = agent.SecretHash);
        var platformToken = scope.ServiceProvider.GetRequiredService<IPlatformSecurity>()
            .IssueToken(actor, DateTimeOffset.UtcNow).Token;
        var agentToken = scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent);
        using var platform = factory.CreateClient();
        platform.DefaultRequestHeaders.Authorization = new("Bearer", platformToken);
        using var adminA = AuthorizedClient(authorized);
        using var adminOther = AuthorizedClient(otherAdmin);
        using var professorClient = AuthorizedClient(professor);
        using var studentClient = AuthorizedClient(student);
        using var adminB = AuthorizedClient(tenantB);
        using var agentClient = factory.CreateClient();
        agentClient.DefaultRequestHeaders.Authorization = new("Bearer", agentToken);

        Assert.Equal(HttpStatusCode.OK, (await professorClient.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await adminA.PutAsJsonAsync($"/api/platform/gyms/{factory.EnabledGym}/maintenance",
                new { allowedAdminUserId = authorized.Id })).StatusCode);
        var control = $"/api/platform/gyms/{factory.EnabledGym}/maintenance";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await platform.PutAsJsonAsync(control, new { allowedAdminUserId = tenantB.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await platform.PutAsJsonAsync(control, new { allowedAdminUserId = student.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await platform.PutAsJsonAsync(control, new { allowedAdminUserId = authorized.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await platform.PutAsJsonAsync(control, new { allowedAdminUserId = authorized.Id })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await adminA.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminA.GetAsync("/api/plans")).StatusCode);
        foreach (var blocked in new[] { adminOther, professorClient, studentClient })
        {
            var response = await blocked.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("GYM_MAINTENANCE", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await adminB.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/health")).StatusCode);
        await using (var restarted = new TestApi { RequiredMaintenanceGymAtStartup = factory.EnabledGym })
        {
            using var resumed = restarted.CreateClient();
            resumed.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(professor));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await resumed.GetAsync("/api/auth/me")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await agentClient.GetAsync("/api/access-agent/control-plane/heartbeat")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await factory.CreateClient().PostAsJsonAsync("/api/access-agents/login",
                new { agentId = agent.Id, gymId = agent.GymId, secret = "synthetic-agent-secret" })).StatusCode);
        var pairingCode = "SYNTHETIC-PAIR-CODE";
        var pairingHash = scope.ServiceProvider.GetRequiredService<IAccessAgentPairingCodeService>().Hash(pairingCode);
        factory.WithDb(db => db.AccessAgentPairingCodes.Add(new AccessAgentPairingCode
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, CreatedByUserId = authorized.Id,
            CodeHash = pairingHash, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await factory.CreateClient().PostAsJsonAsync("/api/access-agents/pair",
                new { code = pairingCode, machineName = "synthetic-second-machine" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await factory.CreateClient().PostAsJsonAsync("/api/students/app-activate/validate",
                new { token = inviteToken })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await factory.CreateClient().PostAsJsonAsync("/api/students/app-activate",
                new { token = inviteToken, password = TestApi.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await factory.CreateClient().PostAsJsonAsync("/api/onboarding/admin-activation",
                new { token = adminInviteToken, password = TestApi.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { student.Email, Password = TestApi.Password })).StatusCode);

        factory.WithDb(db =>
        {
            var state = db.GymMaintenanceStates.Single(x => x.GymId == factory.EnabledGym);
            Assert.True(state.IsActive);
            Assert.Equal(authorized.Id, state.AllowedAdminUserId);
            Assert.Single(db.GymMaintenanceAuditLogs);
            Assert.Null(db.GymMaintenanceStates.SingleOrDefault(x => x.GymId == factory.OtherEnabledGym));
            Assert.Single(db.Students.Where(x => x.AppInviteTokenHash != null));
            Assert.Null(db.AccessAgentPairingCodes.Single(x => x.CodeHash == pairingHash).ConsumedAt);
        });
        Assert.Equal(HttpStatusCode.OK, (await platform.DeleteAsync(control)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await platform.DeleteAsync(control)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await professorClient.GetAsync("/api/auth/me")).StatusCode);
        factory.WithDb(db =>
        {
            var state = db.GymMaintenanceStates.Single(x => x.GymId == factory.EnabledGym);
            Assert.False(state.IsActive);
            Assert.Null(state.AllowedAdminUserId);
            Assert.Equal(2, state.Version);
            Assert.Equal(new[] { "activate", "deactivate" }, db.GymMaintenanceAuditLogs
                .Where(x => x.GymId == factory.EnabledGym).OrderBy(x => x.OccurredAt).Select(x => x.Action).ToArray());
        });
        await using var rejectedStartup = new TestApi { RequiredMaintenanceGymAtStartup = factory.EnabledGym };
        Assert.Throws<InvalidOperationException>(() => rejectedStartup.CreateClient());

        HttpClient AuthorizedClient(User user)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
            return client;
        }
    }
}
