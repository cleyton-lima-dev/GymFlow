using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task MigrationSession_ReadsLegacyFinalResultWithoutChangingItsStoredHistory_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        const string legacyResult = "[{\"entityType\":1,\"rowNumber\":2,\"status\":\"Imported\",\"message\":\"Legacy result\",\"targetId\":null}]";
        factory.WithDb(db =>
        {
            db.MigrationSessions.Add(new MigrationSession
            {
                Id = sessionId,
                GymId = factory.EnabledGym,
                CreatedByUserId = admin.Id,
                Name = "Histórico anterior ao totalizador",
                SourceNamespace = "legacy-source",
                Status = MigrationSessionStatus.Released,
                CreatedAt = now,
                ExpiresAt = now.AddDays(30),
                ReleasedAt = now,
                ReleasedByUserId = admin.Id
            });
            db.MigrationBatches.Add(new MigrationBatch
            {
                Id = batchId,
                MigrationSessionId = sessionId,
                GymId = factory.EnabledGym,
                EntityType = MigrationEntityType.Students,
                Status = MigrationBatchStatus.Released,
                Fingerprint = "legacy-fingerprint",
                ResultJson = legacyResult,
                CreatedAt = now,
                ReleasedAt = now
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var response = await client.GetAsync($"/api/migrations/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        var historicalBatch = session.GetProperty("batches")[0];
        Assert.Equal(1, historicalBatch.GetProperty("rowCount").GetInt32());
        var totals = historicalBatch.GetProperty("totals");
        Assert.Equal(1, totals.GetProperty("successCount").GetInt32());
        Assert.Equal(0, totals.GetProperty("ignoredCount").GetInt32());
        Assert.Equal(0, totals.GetProperty("errorCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, totals.GetProperty("reviewedCount").ValueKind);
        Assert.Equal("Legacy result", session.GetProperty("results")[0].GetProperty("message").GetString());
        factory.WithDb(db => Assert.Equal(legacyResult,
            db.MigrationBatches.Single(batch => batch.Id == batchId).ResultJson));
    }

    [Fact]
    public async Task MigrationSession_PersistsExclusiveOutcomeAndHumanReviewTotalsAcrossAliasesAndReplay_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var firstAliasTarget = Guid.Empty;
        var linkTarget = Guid.NewGuid();
        var ignoreTarget = Guid.NewGuid();
        factory.WithDb(db => db.Students.AddRange(
            new Student { Id = linkTarget, GymId = factory.EnabledGym, Name = "Candidata para vínculo", Email = "link@example.invalid" },
            new Student { Id = ignoreTarget, GymId = factory.EnabledGym, Name = "Candidata para ignorar", Email = "ignore@example.invalid" }));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        async Task<Guid> CreateSession(string name) =>
            (await (await client.PostAsJsonAsync("/api/migrations", new
            {
                name,
                sourceNamespace = "totals-source"
            })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<(Guid BatchId, JsonElement Preview)> Preview(Guid sessionId, object[] rows)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
            {
                entityType = (int)MigrationEntityType.Students,
                rows
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (preview.GetProperty("batchId").GetGuid(), preview);
        }

        async Task Confirm(Guid sessionId, Guid batchId, JsonElement preview, object[] decisions)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
            {
                fingerprint = preview.GetProperty("fingerprint").GetString(),
                decisions
            });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var firstSession = await CreateSession("Criar alias inicial");
        var first = await Preview(firstSession,
            [new { rowNumber = 2, externalId = "known-alias", name = "Aluno do alias" }]);
        await Confirm(firstSession, first.BatchId, first.Preview,
            [new { rowNumber = 2, action = "ImportNew" }]);
        var firstRelease = await client.PostAsJsonAsync($"/api/migrations/{firstSession}/release", new { });
        Assert.Equal(HttpStatusCode.OK, firstRelease.StatusCode);
        var firstResult = await firstRelease.Content.ReadFromJsonAsync<JsonElement>();
        firstAliasTarget = firstResult.GetProperty("results")[0].GetProperty("targetId").GetGuid();

        var session = await CreateSession("Totais mistos com alias");
        var mixed = await Preview(session,
        [
            new { rowNumber = 2, externalId = "new-student", name = "Aluno novo" },
            new { rowNumber = 3, externalId = "known-alias", name = "Aluno do alias" },
            new { rowNumber = 4, externalId = "link-student", name = "Candidata para vínculo", email = "link@example.invalid" },
            new { rowNumber = 5, externalId = "ignored-student", name = "Candidata para ignorar", email = "ignore@example.invalid" }
        ]);

        var linkCandidates = mixed.Preview.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("rowNumber").GetInt32() == 4).GetProperty("candidates");
        Assert.Contains(linkCandidates.EnumerateArray(), candidate => candidate.GetProperty("targetId").GetGuid() == linkTarget);
        var ignoreCandidates = mixed.Preview.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("rowNumber").GetInt32() == 5).GetProperty("candidates");
        Assert.Contains(ignoreCandidates.EnumerateArray(), candidate => candidate.GetProperty("targetId").GetGuid() == ignoreTarget);

        object[] decisions =
        [
            new { rowNumber = 2, action = "ImportNew", targetId = (Guid?)null },
            new { rowNumber = 3, action = "ImportNew", targetId = (Guid?)null },
            new { rowNumber = 4, action = "LinkExisting", targetId = (Guid?)linkTarget },
            new { rowNumber = 5, action = "Ignore", targetId = (Guid?)null }
        ];
        await Confirm(session, mixed.BatchId, mixed.Preview, decisions);

        var release = await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { });
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        var released = await release.Content.ReadFromJsonAsync<JsonElement>();
        var batch = Assert.Single(released.GetProperty("batches").EnumerateArray());
        var totals = batch.GetProperty("totals");
        Assert.Equal(2, totals.GetProperty("successCount").GetInt32());
        Assert.Equal(2, totals.GetProperty("ignoredCount").GetInt32());
        Assert.Equal(0, totals.GetProperty("errorCount").GetInt32());
        Assert.Equal(3, totals.GetProperty("reviewedCount").GetInt32());
        Assert.Equal(4, batch.GetProperty("rowCount").GetInt32());

        var results = released.GetProperty("results").EnumerateArray().ToDictionary(
            row => row.GetProperty("rowNumber").GetInt32());
        Assert.Equal("Imported", results[2].GetProperty("status").GetString());
        Assert.False(results[2].GetProperty("requiredHumanReview").GetBoolean());
        Assert.Equal("AlreadyLinked", results[3].GetProperty("status").GetString());
        Assert.Equal(firstAliasTarget, results[3].GetProperty("targetId").GetGuid());
        Assert.True(results[3].GetProperty("requiredHumanReview").GetBoolean());
        Assert.Equal("Linked", results[4].GetProperty("status").GetString());
        Assert.True(results[4].GetProperty("requiredHumanReview").GetBoolean());
        Assert.Equal("Ignored", results[5].GetProperty("status").GetString());
        Assert.True(results[5].GetProperty("requiredHumanReview").GetBoolean());

        var persistedResultJson = "";
        factory.WithDb(db => persistedResultJson = db.MigrationBatches.Single(item => item.Id == mixed.BatchId).ResultJson);
        using (var persisted = JsonDocument.Parse(persistedResultJson))
        {
            Assert.Equal(JsonValueKind.Object, persisted.RootElement.ValueKind);
            Assert.Equal(2, persisted.RootElement.GetProperty("totals").GetProperty("successCount").GetInt32());
            Assert.Equal(2, persisted.RootElement.GetProperty("totals").GetProperty("ignoredCount").GetInt32());
            Assert.Equal(3, persisted.RootElement.GetProperty("totals").GetProperty("reviewedCount").GetInt32());
        }

        var replay = await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(totals.GetRawText(), replayed.GetProperty("batches")[0].GetProperty("totals").GetRawText());
        Assert.Equal(4, replayed.GetProperty("results").GetArrayLength());

        var reopenedResponse = await client.GetAsync($"/api/migrations/{session}");
        Assert.Equal(HttpStatusCode.OK, reopenedResponse.StatusCode);
        var reopened = await reopenedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reopenedBatch = Assert.Single(reopened.GetProperty("batches").EnumerateArray());
        Assert.Equal("Released", reopenedBatch.GetProperty("status").GetString());
        Assert.Equal(4, reopenedBatch.GetProperty("rowCount").GetInt32());
        Assert.Equal(totals.GetRawText(), reopenedBatch.GetProperty("totals").GetRawText());
        Assert.Equal(4, reopened.GetProperty("results").GetArrayLength());
    }
}
