using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.DTOs.Students;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task StudentImport_ReviewsDuplicatesIsolatesGymAndIsIdempotent_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        var existing = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Cadastro existente",
            Email = "same@example.test", IsActive = false, AppAccessEnabled = false,
            ArchivedAt = DateTime.UtcNow.AddDays(-3)
        };
        var nameOnly = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Marina Souza",
            IsActive = true, AppAccessEnabled = false
        };
        var foreignMatch = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.OtherEnabledGym, Name = "Outro tenant",
            Phone = "5511888777666", IsActive = true, AppAccessEnabled = false
        };
        factory.WithDb(db => db.Students.AddRange(existing, nameOnly, foreignMatch));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var rows = new List<StudentImportInputRow>
        {
            new(2, "Novo sem User", null, null, null),
            new(3, "Mesmo e-mail", "SAME@example.test", null, null),
            new(4, "Marina Souza", null, "11987654321", null),
            new(5, "Mesmo telefone", null, "11987654321", null),
            new(6, "Contato de outro tenant", null, "5511888777666", null),
            new(7, "Nome inválido", "not-an-email", null, null)
        };
        var response = await client.PostAsJsonAsync("/api/students/imports/preview", new { rows });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        var previewRows = preview.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(6, previewRows.Length);
        Assert.Empty(previewRows[0].GetProperty("candidates").EnumerateArray()); // Name is never identity.
        Assert.Equal(existing.Id, previewRows[1].GetProperty("candidates")[0].GetProperty("studentId").GetGuid());
        Assert.True(previewRows[1].GetProperty("candidates")[0].GetProperty("isArchived").GetBoolean());
        Assert.Equal(1, previewRows[2].GetProperty("candidates").GetArrayLength());
        Assert.Equal(5, previewRows[2].GetProperty("candidates")[0].GetProperty("sourceRowNumber").GetInt32());
        Assert.Empty(previewRows[4].GetProperty("candidates").EnumerateArray()); // Contacts in another gym are invisible.
        Assert.False(previewRows[5].GetProperty("isValid").GetBoolean());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", new { newStudentsActive = true, decisions = Array.Empty<object>() })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var decisions = new[]
        {
            new { rowNumber = 2, action = "Import" },
            new { rowNumber = 3, action = "Ignore" },
            new { rowNumber = 4, action = "ImportAsNew" },
            new { rowNumber = 5, action = "Ignore" },
            new { rowNumber = 6, action = "Import" }
        };
        var confirmationBody = new { newStudentsActive = true, decisions };
        var confirmation = await client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", confirmationBody);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var result = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, result.GetProperty("results").EnumerateArray().Count(
            row => row.GetProperty("status").GetString() == "Imported"));
        Assert.Equal(2, result.GetProperty("results").EnumerateArray().Count(
            row => row.GetProperty("status").GetString() == "Ignored"));
        Assert.Equal(1, result.GetProperty("results").EnumerateArray().Count(
            row => row.GetProperty("status").GetString() == "Error"));

        var repeated = await client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", confirmationBody);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.True((await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alreadyCompleted").GetBoolean());

        factory.WithDb(db =>
        {
            var imported = db.Students.Where(student => student.Name == "Novo sem User" ||
                student.Name == "Contato de outro tenant" ||
                student.Name == "Marina Souza" && student.Id != nameOnly.Id).ToList();
            Assert.Equal(3, imported.Count);
            Assert.All(imported, student =>
            {
                Assert.Equal(factory.EnabledGym, student.GymId);
                Assert.Null(student.UserId);
                Assert.False(student.AppAccessEnabled);
                Assert.True(student.IsActive);
            });
            var unchanged = db.Students.Single(student => student.Id == existing.Id);
            Assert.False(unchanged.IsActive);
            Assert.False(unchanged.AppAccessEnabled);
            Assert.NotNull(unchanged.ArchivedAt);
            Assert.Equal(2, db.Students.Count(student => student.Name == "Marina Souza"));
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.Empty(db.StudentImportBatches.AsNoTracking().Where(batch => batch.PayloadJson != null || batch.PreviewJson != null));
        });
    }

    [Fact]
    public async Task StudentImport_RequiresAdminAndExplicitDuplicateDecision_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var student = factory.AddUser(factory.EnabledGym);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(student));
        var forbidden = await client.PostAsJsonAsync("/api/students/imports/preview", new
        {
            rows = new[] { new { rowNumber = 2, name = "Tentativa", email = (string?)null, phone = (string?)null, birthDate = (string?)null } }
        });
        Assert.Contains(forbidden.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var previewResponse = await client.PostAsJsonAsync("/api/students/imports/preview", new
        {
            rows = new[]
            {
                new { rowNumber = 2, name = "Candidate", email = student.Email, phone = (string?)null, birthDate = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var batchId = (await previewResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("batchId").GetGuid();
        var noDecision = await client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", new
        {
            newStudentsActive = true, decisions = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, noDecision.StatusCode);
        factory.WithDb(db => Assert.Single(db.Students.Where(item => item.Email == student.Email)));
    }

    [Fact]
    public async Task StudentImport_ConcurrentConfirmationCreatesAtMostOneStudent_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var preview = await client.PostAsJsonAsync("/api/students/imports/preview", new
        {
            rows = new[]
            {
                new { rowNumber = 2, name = "Concorrência", email = (string?)null, phone = (string?)null, birthDate = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var batchId = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("batchId").GetGuid();
        var body = new
        {
            newStudentsActive = true,
            decisions = new[] { new { rowNumber = 2, action = "Import" } }
        };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", body),
            client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", body));
        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected confirmation status {(int)response.StatusCode}. Captured server exceptions: {factory.CapturedExceptions}"));
        factory.WithDb(db => Assert.Single(db.Students.Where(student => student.Name == "Concorrência")));
    }
}
