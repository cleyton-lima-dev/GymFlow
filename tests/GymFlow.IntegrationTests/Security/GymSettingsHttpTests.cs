using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("Student", 403)]
    [InlineData("Professor", 403)]
    [InlineData("AccessAgent", 403)]
    [InlineData("inactive", 401)]
    public async Task GymSettings_RequiresActiveAdminThroughJwt(string identity, int expected)
    {
        await using var factory = new TestApi();
        using var client = factory.CreateClient();
        if (identity == "AccessAgent")
        {
            var agent = new AccessAgent { Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic", SecretHash = "test-only" };
            factory.WithDb(db => db.AccessAgents.Add(agent));
            using var scope = factory.Services.CreateScope();
            client.DefaultRequestHeaders.Authorization = new("Bearer", scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        }
        else if (identity != "anonymous")
        {
            var user = factory.AddUser(factory.LegacyGym, identity == "Professor" ? UserRole.Professor : identity == "inactive" ? UserRole.Admin : UserRole.Student, identity != "inactive");
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        }
        Assert.Equal((HttpStatusCode)expected, (await client.GetAsync("/api/gym-settings")).StatusCode);
        Assert.Equal((HttpStatusCode)expected, (await client.PutAsJsonAsync("/api/gym-settings", new { AdministrativeName = "Synthetic", TimeZoneId = "UTC" })).StatusCode);
        Assert.Equal((HttpStatusCode)expected, (await client.GetAsync("/api/access-agents")).StatusCode);
        Assert.Equal((HttpStatusCode)expected, (await client.PatchAsJsonAsync($"/api/access-agents/{Guid.NewGuid()}/release", new { enabled = false })).StatusCode);
    }

    [Theory]
    [InlineData("UTC", "configured")]
    [InlineData("America/Sao_Paulo", "configured")]
    [InlineData(null, "missing")]
    [InlineData(" ", "missing")]
    [InlineData("invalid-synthetic-time-zone", "invalid")]
    public async Task GymSettings_DistinguishesTimeZoneConfiguration(string? value, string expected)
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        configuration[$"GymTimeZones:{factory.LegacyGym:D}"] = value;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var response = await client.GetAsync("/api/gym-settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("timeZoneStatus").GetString());
        Assert.Equal(5, body.EnumerateObject().Count());
        Assert.Equal(expected == "configured", body.GetProperty("timeZoneId").ValueKind == JsonValueKind.String);
        Assert.Equal(value, configuration[$"GymTimeZones:{factory.LegacyGym:D}"]);
    }

    [Fact]
    public async Task GymSettings_UsesAuthenticatedGymAndRejectsUnimplementedWrites()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var otherAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var student = factory.AddUser(factory.LegacyGym);
        var enrollmentId = factory.AddEnrollment(student, factory.LegacyGym, EnrollmentStatus.Active, -1, 20);
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        configuration[$"GymTimeZones:{factory.EnabledGym:D}"] = "America/Sao_Paulo";
        DateOnly before = default;
        factory.WithDb(db => before = db.Enrollments.Single(e => e.Id == enrollmentId).EndDate);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var own = await client.GetFromJsonAsync<JsonElement>($"/api/gym-settings?gymId={factory.EnabledGym}");
        Assert.Equal("UTC", own.GetProperty("timeZoneId").GetString());
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Post })
        {
            using var request = new HttpRequestMessage(method, "/api/gym-settings")
            {
                Content = JsonContent.Create(new { gymId = factory.EnabledGym, name = "Must not save", timeZoneId = "Pacific/Honolulu", erpEnabled = true, secretHash = "Must not save" })
            };
            Assert.Equal(method == HttpMethod.Put ? HttpStatusCode.BadRequest : HttpStatusCode.MethodNotAllowed, (await client.SendAsync(request)).StatusCode);
        }
        Assert.Equal("UTC", configuration[$"GymTimeZones:{factory.LegacyGym:D}"]);
        Assert.Null(configuration[$"GymFeatures:{factory.LegacyGym:D}:ErpEnabled"]);
        Assert.Equal("true", configuration[$"GymFeatures:{factory.EnabledGym:D}:ErpEnabled"]); // Synthetic fixture, unchanged.
        factory.WithDb(db => Assert.Equal(before, db.Enrollments.Single(e => e.Id == enrollmentId).EndDate));
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var foreign = await client.GetFromJsonAsync<JsonElement>("/api/gym-settings");
        Assert.NotEqual("UTC", foreign.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task GymSettings_ExistingOperationalControlPersistsWithoutChangingCredentialsOrOtherTenants()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var own = new AccessAgent { Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic own", ReleaseEnabled = true, SecretHash = "synthetic-secret-not-exported" };
        var other = new AccessAgent { Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Synthetic other", ReleaseEnabled = true, SecretHash = "synthetic-secret-not-exported" };
        factory.WithDb(db => db.AccessAgents.AddRange(own, other));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var listed = await client.GetFromJsonAsync<JsonElement>($"/api/access-agents?gymId={factory.EnabledGym}");
        Assert.Single(listed.EnumerateArray());
        Assert.DoesNotContain("Synthetic other", listed.ToString());
        Assert.DoesNotContain("synthetic-secret", listed.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/access-agents/{other.Id}/release", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/access-agents/{own.Id}/release", new { enabled = false, secretHash = "Must not save", credentialVersion = 99, erpEnabled = true })).StatusCode);
        factory.WithDb(db =>
        {
            var saved = db.AccessAgents.Single(a => a.Id == own.Id);
            Assert.False(saved.ReleaseEnabled);
            Assert.Equal(2, saved.ConfigurationVersion);
            Assert.Equal(1, saved.CredentialVersion);
            Assert.Equal("synthetic-secret-not-exported", saved.SecretHash);
            Assert.True(db.AccessAgents.Single(a => a.Id == other.Id).ReleaseEnabled);
            Assert.Single(db.AccessAgentAuditLogs);
        });
    }
}
