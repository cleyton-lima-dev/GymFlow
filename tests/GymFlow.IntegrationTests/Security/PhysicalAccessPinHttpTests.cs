using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task PinIndexRejectsTwoCredentialsForOneStudentAcrossProviders()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym,
            Name = "Synthetic PIN index student", IsActive = true
        }));
        factory.WithDb(db => db.PhysicalAccessCredentials.Add(new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, StudentId = studentId,
            Type = PhysicalAccessCredentialType.PinExternalId,
            ProviderKey = "synthetic-a", ExternalIdentifier = "pin:v1:synthetic-a"
        }));

        Assert.Throws<DbUpdateException>(() => factory.WithDb(db =>
            db.PhysicalAccessCredentials.Add(new PhysicalAccessCredential
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, StudentId = studentId,
                Type = PhysicalAccessCredentialType.PinExternalId,
                ProviderKey = "synthetic-b", ExternalIdentifier = "pin:v1:synthetic-b"
            })));
    }

    [Fact]
    public async Task PinIssuanceIsAdminOnlyTenantScopedAndNeverListed()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var otherAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        var professor = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        var studentId = Guid.NewGuid();
        var secondStudentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym,
            Name = "Fictitious PIN student", IsActive = true
        }));
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = secondStudentId, GymId = factory.EnabledGym,
            Name = "Second fictitious PIN student", IsActive = true
        }));
        using var client = factory.CreateClient();
        var path = $"/api/physical-access/credentials/student/{studentId}/pin";

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(professor));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(path, new { })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(path, new { })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/physical-access/credentials", new
            {
                studentId, type = PhysicalAccessCredentialType.PinExternalId,
                providerKey = "toletus-litenet2", externalIdentifier = "0007"
            })).StatusCode);

        var issued = await client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var pin = issuedJson.RootElement.GetProperty("pin").GetString()!;
        Assert.Matches("^[0-9]{4}$", pin);

        var listed = await client.GetAsync(
            $"/api/physical-access/credentials/student/{studentId}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var listedBody = await listed.Content.ReadAsStringAsync();
        Assert.DoesNotContain(pin, listedBody);
        using var listedJson = JsonDocument.Parse(listedBody);
        Assert.Equal(string.Empty,
            listedJson.RootElement[0].GetProperty("externalIdentifier").GetString());

        factory.WithDb(db =>
        {
            var stored = db.PhysicalAccessCredentials.Single(item => item.StudentId == studentId);
            Assert.NotEqual(pin, stored.ExternalIdentifier);
            Assert.StartsWith("pin:v1:", stored.ExternalIdentifier);
        });

        object decisionRequest() => new
        {
            requestId = Guid.NewGuid(), providerKey = "toletus-litenet2",
            credentialType = PhysicalAccessCredentialType.PinExternalId,
            externalIdentifier = pin, occurredAt = DateTime.UtcNow
        };
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var foreignDecision = await client.PostAsJsonAsync(
            "/api/physical-access/decisions", decisionRequest());
        Assert.Equal(HttpStatusCode.OK, foreignDecision.StatusCode);
        using (var foreignJson = JsonDocument.Parse(await foreignDecision.Content.ReadAsStringAsync()))
            Assert.Equal(2, foreignJson.RootElement.GetProperty("decision").GetInt32());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var localDecision = await client.PostAsJsonAsync(
            "/api/physical-access/decisions", decisionRequest());
        Assert.Equal(HttpStatusCode.OK, localDecision.StatusCode);
        using (var localJson = JsonDocument.Parse(await localDecision.Content.ReadAsStringAsync()))
            Assert.Equal(1, localJson.RootElement.GetProperty("decision").GetInt32());

        var wrongPin = pin == "9999" ? "9998" : "9999";
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var denied = await client.PostAsJsonAsync(
                "/api/physical-access/decisions", new
                {
                    requestId = Guid.NewGuid(), providerKey = "toletus-litenet2",
                    credentialType = PhysicalAccessCredentialType.PinExternalId,
                    externalIdentifier = wrongPin, occurredAt = DateTime.UtcNow
                });
            Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
            using var deniedJson = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            Assert.Equal(2, deniedJson.RootElement.GetProperty("decision").GetInt32());
            Assert.Equal(attempt == 5 ? 109 : 100,
                deniedJson.RootElement.GetProperty("reason").GetInt32());
        }

        var blockedCorrectPin = await client.PostAsJsonAsync(
            "/api/physical-access/decisions", decisionRequest());
        using (var blockedJson = JsonDocument.Parse(await blockedCorrectPin.Content.ReadAsStringAsync()))
            Assert.Equal(109, blockedJson.RootElement.GetProperty("reason").GetInt32());

        var reset = await client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        using var resetJson = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
        Assert.NotEqual(pin, resetJson.RootElement.GetProperty("pin").GetString());
        var secondIssue = await client.PostAsJsonAsync(
            $"/api/physical-access/credentials/student/{secondStudentId}/pin", new { });
        Assert.Equal(HttpStatusCode.OK, secondIssue.StatusCode);
        using var secondJson = JsonDocument.Parse(await secondIssue.Content.ReadAsStringAsync());
        Assert.NotEqual(resetJson.RootElement.GetProperty("pin").GetString(),
            secondJson.RootElement.GetProperty("pin").GetString());
        factory.WithDb(db => Assert.Equal(2, db.PhysicalAccessCredentials.Count()));
    }
}
