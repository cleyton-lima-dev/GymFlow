using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GymFlow.Domain.Enums;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task MigrationSession_OfficialCsvModelsAreAcceptedByPreview_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var sessionResponse = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Modelos oficiais sintéticos", sourceNamespace = "official-template-test"
        });
        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);
        var sessionId = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var templates = new[]
        {
            (FileName: "modelo-alunos.csv", Type: MigrationEntityType.Students),
            (FileName: "modelo-planos.csv", Type: MigrationEntityType.Plans),
            (FileName: "modelo-matriculas.csv", Type: MigrationEntityType.Enrollments),
            (FileName: "modelo-situacao-financeira.csv", Type: MigrationEntityType.Charges)
        };

        foreach (var template in templates)
        {
            var lines = await File.ReadAllLinesAsync(
                Path.Combine(AppContext.BaseDirectory, "import_templates", template.FileName), Encoding.UTF8);
            Assert.Equal(2, lines.Length);
            var headers = lines[0].Split(',');
            var values = lines[1].Split(',');
            Assert.Equal(headers.Length, values.Length);
            var row = headers.Select((header, index) => new { header, value = values[index] })
                .ToDictionary(item => item.header, item => (object?)item.value);
            row["rowNumber"] = 2;

            var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
            {
                entityType = (int)template.Type,
                rows = new[] { row }
            });
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
            var previewRow = Assert.Single(preview.GetProperty("rows").EnumerateArray());
            Assert.True(previewRow.GetProperty("isValid").GetBoolean(),
                $"{template.FileName}: {previewRow.GetProperty("errors")}");

            if (template.Type == MigrationEntityType.Charges) continue;
            var batchId = preview.GetProperty("batchId").GetGuid();
            var confirmResponse = await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                {
                    fingerprint = preview.GetProperty("fingerprint").GetString(),
                    decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
                });
            Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);
        }
    }
}
