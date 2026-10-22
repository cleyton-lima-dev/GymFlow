using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task MigrationSession_SeparateFinancialBatchResolvesConfirmedUnreleasedEnrollmentWithoutRevenue_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var otherGymAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        var professor = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessionResponse = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Situação financeira separada", sourceNamespace = "finance-file"
        });
        var sessionId = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task Confirm(int type, object[] rows)
        {
            var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = type, rows });
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
            var batchId = preview.GetProperty("batchId").GetGuid();
            var decisions = preview.GetProperty("rows").EnumerateArray().Select(row => new
            {
                rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew"
            }).ToArray();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions })).StatusCode);
        }

        await Confirm(1, [new { rowNumber = 2, externalId = "student-fin-1", name = "Aluno financeiro sintético" }]);
        await Confirm(2, [new { rowNumber = 2, externalId = "plan-fin-1", name = "Plano financeiro sintético", price = "120,00", durationMonths = "12", billingCycle = "monthly" }]);
        await Confirm(3, [new
        {
            rowNumber = 2, externalId = "enrollment-fin-1", studentExternalId = "student-fin-1",
            planExternalId = "plan-fin-1", startDate = "2026-01-01", endDate = "2027-01-01"
        }]);

        var chargeRows = new[]
        {
            new { rowNumber = 2, externalId = "charge-fin-1", studentExternalId = "student-fin-1",
                enrollmentExternalId = "enrollment-fin-1", chargeState = "Paid" }
        };
        var financePreviewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows = chargeRows });
        Assert.Equal(HttpStatusCode.OK, financePreviewResponse.StatusCode);
        var financePreview = await financePreviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(financePreview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        var financeBatchId = financePreview.GetProperty("batchId").GetGuid();

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(professor));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{financeBatchId}/confirm",
            new { decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherGymAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows = chargeRows })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var repeated = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows = chargeRows });
        var repeatedPreview = await repeated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(repeatedPreview.GetProperty("alreadyStaged").GetBoolean());
        Assert.Equal(financeBatchId, repeatedPreview.GetProperty("batchId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{financeBatchId}/confirm",
            new { fingerprint = repeatedPreview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);

        factory.WithDb(db =>
        {
            var student = db.Students.Single(item => item.Name == "Aluno financeiro sintético");
            var enrollment = db.Enrollments.Single(item => item.StudentId == student.Id);
            var charge = db.Charges.Single(item => item.EnrollmentId == enrollment.Id);
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
            Assert.Equal(ChargeStatus.Paid, charge.Status);
            Assert.Equal(ChargeOrigin.Migration, charge.Origin);
            Assert.Equal(PaymentOrigin.Migration, charge.PaymentOrigin);
            Assert.Null(charge.PaidAmount);
            Assert.Null(charge.PaidAt);
            Assert.Empty(db.FinancialAuditLogs.Where(item => item.Action == FinancialAuditAction.PaymentConfirmed));
            Assert.Single(db.MigrationExternalReferences.Where(reference => reference.GymId == factory.EnabledGym &&
                reference.EntityType == MigrationEntityType.Charges && reference.ExternalId == "charge-fin-1"));
        });
    }

    [Fact]
    public async Task MigrationSession_SeparateFinancialBatchRequiresExplicitEnrollmentAndConflictResolution_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessionResponse = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Resolução financeira sintética", sourceNamespace = "finance-conflict"
        });
        var sessionId = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<Guid> StageAndConfirm(int type, object[] rows)
        {
            var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = type, rows });
            var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
            var batchId = preview.GetProperty("batchId").GetGuid();
            var decisions = preview.GetProperty("rows").EnumerateArray().Select(row => new
            {
                rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew"
            }).ToArray();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions })).StatusCode);
            return batchId;
        }

        await StageAndConfirm(1, [new { rowNumber = 2, externalId = "student-multi", name = "Aluno com matrículas múltiplas" }]);
        await StageAndConfirm(2, [new { rowNumber = 2, externalId = "plan-multi", name = "Plano múltiplo", price = "80,00", durationMonths = "1", billingCycle = "monthly" }]);
        var firstStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstEnd = firstStart.AddMonths(1);
        var secondEnd = firstEnd.AddMonths(1);
        await StageAndConfirm(3,
        [
            new { rowNumber = 2, externalId = "enrollment-multi-a", studentExternalId = "student-multi", planExternalId = "plan-multi", startDate = firstStart.ToString("yyyy-MM-dd"), endDate = firstEnd.ToString("yyyy-MM-dd"), chargeState = "Pending" },
            new { rowNumber = 3, externalId = "enrollment-multi-b", studentExternalId = "student-multi", planExternalId = "plan-multi", startDate = firstEnd.ToString("yyyy-MM-dd"), endDate = secondEnd.ToString("yyyy-MM-dd"), chargeState = "Pending" }
        ]);

        var ambiguousResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 4,
            rows = new[] { new { rowNumber = 2, externalId = "charge-ambiguous", studentExternalId = "student-multi", chargeState = "Paid" } }
        });
        var ambiguous = await ambiguousResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(ambiguous.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        Assert.Contains(ambiguous.GetProperty("rows")[0].GetProperty("errors").EnumerateArray(), error =>
            error.GetString()!.Contains("múltiplas matrículas", StringComparison.Ordinal));
        var ambiguousBatchId = ambiguous.GetProperty("batchId").GetGuid();

        var missingResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 4,
            rows = new[] { new { rowNumber = 2, externalId = "charge-missing", studentExternalId = "missing-student", enrollmentExternalId = "missing-enrollment", chargeState = "Paid" } }
        });
        var missing = await missingResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(missing.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        var missingBatchId = missing.GetProperty("batchId").GetGuid();

        var conflictResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 4,
            rows = new[] { new { rowNumber = 2, externalId = "charge-conflict", studentExternalId = "student-multi", enrollmentExternalId = "enrollment-multi-a", chargeState = "Paid" } }
        });
        var conflict = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conflictRow = conflict.GetProperty("rows")[0];
        Assert.False(conflictRow.GetProperty("isValid").GetBoolean());
        Assert.True(conflictRow.GetProperty("requiresFinancialResolution").GetBoolean());
        var conflictBatchId = conflict.GetProperty("batchId").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{conflictBatchId}/confirm",
            new { fingerprint = await CurrentBatchFingerprint(client, sessionId, conflictBatchId),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{conflictBatchId}/confirm",
            new { fingerprint = await CurrentBatchFingerprint(client, sessionId, conflictBatchId),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew", financialStateResolution = "UseFinancialFile" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{ambiguousBatchId}/discard", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{missingBatchId}/discard", new { })).StatusCode);
        var releaseResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        var release = await releaseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conflictBatch = release.GetProperty("batches").EnumerateArray()
            .Single(batch => batch.GetProperty("batchId").GetGuid() == conflictBatchId);
        Assert.Equal(1, conflictBatch.GetProperty("totals").GetProperty("successCount").GetInt32());
        Assert.Equal(0, conflictBatch.GetProperty("totals").GetProperty("ignoredCount").GetInt32());
        Assert.Equal(0, conflictBatch.GetProperty("totals").GetProperty("errorCount").GetInt32());
        Assert.Equal(1, conflictBatch.GetProperty("totals").GetProperty("reviewedCount").GetInt32());
        var conflictResult = release.GetProperty("results").EnumerateArray().Single(result =>
            result.GetProperty("externalId").GetString() == "charge-conflict");
        Assert.True(conflictResult.GetProperty("requiredHumanReview").GetBoolean());
        factory.WithDb(db =>
        {
            var student = db.Students.Single(item => item.Name == "Aluno com matrículas múltiplas");
            var paidEnrollment = db.Enrollments.Single(item => item.StudentId == student.Id && item.StartDate == firstStart);
            var paidCharge = db.Charges.Single(item => item.EnrollmentId == paidEnrollment.Id);
            Assert.Equal(ChargeStatus.Paid, paidCharge.Status);
            Assert.Null(paidCharge.PaidAmount);
            Assert.Null(paidCharge.PaidAt);
            Assert.Equal(EnrollmentStatus.Active, paidEnrollment.Status);
        });
    }

    [Fact]
    public async Task MigrationBatch_ReopensPersistedPreviewForReviewWithAdminGymIsolation_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var nonAdmin = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var created = await client.PostAsJsonAsync("/api/migrations", new { name = "Revisão retomável", sourceNamespace = "resume-review" });
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 2,
            rows = new[]
            {
                new { rowNumber = 2, externalId = "resume-plan-1", name = "Plano sintético A", price = "100,00", durationMonths = "1", billingCycle = "monthly" },
                new { rowNumber = 3, externalId = "resume-plan-2", name = "Plano sintético B", price = "200,00", durationMonths = "3", billingCycle = "quarterly" }
            }
        });
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        var decisions = new[]
        {
            new { rowNumber = 2, action = "ImportNew" },
            new { rowNumber = 3, action = "ImportNew" }
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/review", new
            { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions })).StatusCode);

        using var restartedClient = factory.CreateClient();
        restartedClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var resumedResponse = await restartedClient.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review");
        Assert.Equal(HttpStatusCode.OK, resumedResponse.StatusCode);
        var resumed = await resumedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(batchId, resumed.GetProperty("batchId").GetGuid());
        Assert.True(resumed.GetProperty("alreadyStaged").GetBoolean());
        Assert.Equal(2, resumed.GetProperty("rows").GetArrayLength());
        Assert.Equal("resume-plan-1", resumed.GetProperty("rows")[0].GetProperty("values").GetProperty("externalId").GetString());
        Assert.Equal("ImportNew", resumed.GetProperty("decisions")[0].GetProperty("action").GetString());
        Assert.Equal("ImportNew", resumed.GetProperty("decisions")[1].GetProperty("action").GetString());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(nonAdmin));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/review", new { decisions })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
            { fingerprint = await CurrentBatchFingerprint(client, sessionId, batchId), decisions })).StatusCode);
        var confirmedPlanReview = await client.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review");
        Assert.Equal(HttpStatusCode.OK, confirmedPlanReview.StatusCode);
        Assert.Equal(batchId, (await confirmedPlanReview.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("batchId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review")).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(2, db.Plans.Count(plan => plan.GymId == factory.EnabledGym && plan.Name.StartsWith("Plano sintético")));
            Assert.Empty(db.Plans.Where(plan => plan.GymId == factory.OtherEnabledGym && plan.Name.StartsWith("Plano sintético")));
        });
    }

    [Fact]
    public async Task MigrationSession_ListRestoresPersistedBatchesAndIsScopedToGym_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var created = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Sessão recuperável", sourceNamespace = "resume-source"
        });
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var staged = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "resume-student", name = "Aluno sintético retomável" } }
        });
        var batchId = (await staged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("batchId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/confirm",
            new { fingerprint = await CurrentBatchFingerprint(client, sessionId, batchId),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        var pendingBatchResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, name = "Aluno sintético com erro" } }
        });
        Assert.Equal(HttpStatusCode.OK, pendingBatchResponse.StatusCode);
        var pendingBatchId = (await pendingBatchResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("batchId").GetGuid();

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        var otherCreated = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Outra academia", sourceNamespace = "other-source"
        });
        var otherSessionId = (await otherCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var foreignList = await client.GetFromJsonAsync<JsonElement[]>("/api/migrations");
        Assert.Single(foreignList!);
        Assert.Equal(otherSessionId, foreignList![0].GetProperty("sessionId").GetGuid());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessions = await client.GetFromJsonAsync<JsonElement[]>("/api/migrations");
        Assert.Single(sessions!);
        Assert.Equal(sessionId, sessions![0].GetProperty("sessionId").GetGuid());
        Assert.Equal(2, sessions[0].GetProperty("batchCount").GetInt32());
        var resumed = await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{sessionId}");
        var resumedStatuses = resumed.GetProperty("batches").EnumerateArray()
            .ToDictionary(batch => batch.GetProperty("batchId").GetGuid(),
                batch => batch.GetProperty("status").GetString());
        Assert.Equal("Confirmed", resumedStatuses[batchId]);
        Assert.Equal("Previewed", resumedStatuses[pendingBatchId]);
    }

    [Fact]
    public async Task MigrationSession_DiscardPreviewedBatchUnblocksReleaseAndPreservesConfirmedBatch_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var created = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Descarte sintético", sourceNamespace = "discard-source"
        });
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var invalidResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, name = "Aluno sintético inválido" } }
        });
        var invalid = await invalidResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invalidBatchId = invalid.GetProperty("batchId").GetGuid();
        Assert.False(invalid.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());

        var validResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "valid-student", name = "Aluno sintético válido" } }
        });
        var valid = await validResponse.Content.ReadFromJsonAsync<JsonElement>();
        var validBatchId = valid.GetProperty("batchId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{validBatchId}/confirm",
            new { fingerprint = valid.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{invalidBatchId}/discard", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{invalidBatchId}/discard", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{invalidBatchId}/discard", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{validBatchId}/discard", new { })).StatusCode);

        var details = await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{sessionId}");
        Assert.True(details.GetProperty("canRelease").GetBoolean(),
            string.Join(" | ", details.GetProperty("releaseBlockers").EnumerateArray().Select(item => item.GetString())));
        var batchSummaries = details.GetProperty("batches").EnumerateArray().ToList();
        Assert.Contains(batchSummaries, item => item.GetProperty("batchId").GetGuid() == invalidBatchId &&
            item.GetProperty("status").GetString() == "Discarded" && item.GetProperty("rowCount").GetInt32() == 1);
        Assert.Contains(batchSummaries, item => item.GetProperty("batchId").GetGuid() == validBatchId &&
            item.GetProperty("status").GetString() == "Confirmed");
        factory.WithDb(db =>
        {
            var discardedBatch = db.MigrationBatches.Single(batch => batch.Id == invalidBatchId);
            Assert.Equal("[]", discardedBatch.PayloadJson);
            Assert.DoesNotContain("Aluno sintético inválido", discardedBatch.PreviewJson);
            Assert.Equal(MigrationBatchStatus.Discarded, discardedBatch.Status);
        });

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Empty(db.Students.Where(student => student.Name == "Aluno sintético inválido"));
            Assert.Single(db.Students.Where(student => student.Name == "Aluno sintético válido"));
            Assert.DoesNotContain(db.MigrationExternalReferences, reference => reference.ExternalId != "valid-student");
        });
    }

    [Fact]
    public async Task MigrationSession_StagesThenReleasesFullContractWithoutRevenueOrCrossTenantAccess_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        var before = 0;
        factory.WithDb(db => before = db.Students.Count());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var created = await client.PostAsJsonAsync("/api/migrations", new { name = "Importação sintética", sourceNamespace = "sistema-legado" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<Guid> Stage(int type, object[] rows)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = type, rows });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.All(preview.GetProperty("rows").EnumerateArray(), row => Assert.True(row.GetProperty("isValid").GetBoolean()));
            var batchId = preview.GetProperty("batchId").GetGuid();
            var decisions = preview.GetProperty("rows").EnumerateArray().Select(row => new
            {
                rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew"
            }).ToArray();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions })).StatusCode);
            return batchId;
        }

        await Stage(1,
        [
            new { rowNumber = 2, externalId = "s-paid", name = "Sintético Quitado" },
            new { rowNumber = 3, externalId = "s-pending", name = "Sintético Pendente" },
            new { rowNumber = 4, externalId = "s-unknown", name = "Sintético Não Verificado" }
        ]);
        await Stage(2,
        [
            new { rowNumber = 2, externalId = "p-month", name = "Plano Legado", price = "123,45", durationMonths = "1", billingCycle = "mensal" }
        ]);
        var start = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1).ToString("yyyy-MM-dd");
        await Stage(3,
        [
            new { rowNumber = 2, externalId = "e-paid", studentExternalId = "s-paid", planExternalId = "p-month", name = "Snapshot Plano", price = "123,45", durationMonths = "1", billingCycle = "mensal", startDate = start, endDate = end, chargeState = "quitada" },
            new { rowNumber = 3, externalId = "e-pending", studentExternalId = "s-pending", planExternalId = "p-month", name = "Snapshot Plano", price = "123,45", durationMonths = "1", billingCycle = "mensal", startDate = start, endDate = end, chargeState = "pendente" },
            new { rowNumber = 4, externalId = "e-unknown", studentExternalId = "s-unknown", planExternalId = "p-month", name = "Snapshot Plano", price = "123,45", durationMonths = "1", billingCycle = "mensal", startDate = start, endDate = end, chargeState = "Unverified" }
        ]);

        factory.WithDb(db =>
        {
            Assert.Equal(before, db.Students.Count());
            Assert.Empty(db.Plans.Where(plan => plan.Name == "Plano Legado"));
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
        });

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/migrations/{sessionId}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{sessionId}");
        Assert.True(details.GetProperty("canRelease").GetBoolean(),
            string.Join(" | ", details.GetProperty("releaseBlockers").EnumerateArray().Select(item => item.GetString())));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(0m, dashboard.GetProperty("receivedRevenue").GetDecimal());

        factory.WithDb(db =>
        {
            var students = db.Students.Where(student => student.Name.StartsWith("Sintético")).ToList();
            Assert.Equal(3, students.Count);
            Assert.All(students, student => { Assert.Null(student.UserId); Assert.False(student.AppAccessEnabled); Assert.Equal(factory.EnabledGym, student.GymId); });
            var enrollments = db.Enrollments.Include(enrollment => enrollment.Charge).Where(enrollment => enrollment.PlanName == "Snapshot Plano").ToList();
            Assert.Equal(3, enrollments.Count);
            Assert.All(enrollments, enrollment => { Assert.Equal(123.45m, enrollment.PlanPrice); Assert.Equal(123.45m, enrollment.Charge!.Amount); Assert.Equal(ChargeOrigin.Migration, enrollment.Charge.Origin); });
            var paid = enrollments.Single(enrollment => enrollment.Charge!.Status == ChargeStatus.Paid).Charge!;
            Assert.Equal(PaymentOrigin.Migration, paid.PaymentOrigin);
            Assert.Null(paid.PaidAmount);
            Assert.Null(paid.PaidAt);
            Assert.All(enrollments.Where(enrollment => enrollment.Charge!.Status == ChargeStatus.Pending), enrollment =>
                Assert.Equal(PaymentOrigin.None, enrollment.Charge!.PaymentOrigin));
            Assert.Single(enrollments, enrollment => enrollment.Charge!.VerificationStatus == ChargeVerificationStatus.Unverified);
            Assert.Empty(db.FinancialAuditLogs.Where(log => log.Action == FinancialAuditAction.PaymentConfirmed));
        });

        var replay = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }

    [Fact]
    public async Task MigrationEnrollment_InheritsConfirmedPlanTermsAndPreservesContractOverrides_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var created = await client.PostAsJsonAsync("/api/migrations", new { name = "Snapshot sintético", sourceNamespace = "snapshot-source" });
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<JsonElement> Preview(int entityType, object[] rows)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType, rows });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rows");
        }

        async Task Confirm(int entityType, object[] rows)
        {
            var preview = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType, rows });
            var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
            var batchId = body.GetProperty("batchId").GetGuid();
            var decisions = body.GetProperty("rows").EnumerateArray().Select(row => new
            {
                rowNumber = row.GetProperty("rowNumber").GetInt32(),
                action = row.GetProperty("isValid").GetBoolean() ? "ImportNew" : "Ignore"
            }).ToArray();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                { fingerprint = body.GetProperty("fingerprint").GetString(), decisions })).StatusCode);
        }

        await Confirm(1,
        [
            new { rowNumber = 2, externalId = "s-inherited", name = "Aluno Herança" },
            new { rowNumber = 3, externalId = "s-override", name = "Aluno Contrato" },
            new { rowNumber = 4, externalId = "s-invalid", name = "Aluno Estado Inválido" },
            new { rowNumber = 5, externalId = "s-empty-state", name = "Aluno Estado Vazio" }
        ]);
        await Confirm(2,
        [
            new { rowNumber = 2, externalId = "p-confirmed", name = "Plano Confirmado", price = "120,00", durationMonths = "3", billingCycle = "quarterly" }
        ]);

        var start = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3).ToString("yyyy-MM-dd");
        object[] enrollmentRows =
        [
            new { rowNumber = 2, externalId = "e-inherited", studentExternalId = "s-inherited", planExternalId = "p-confirmed", startDate = start, endDate = end, chargeState = "Unverified" },
            new { rowNumber = 3, externalId = "e-override", studentExternalId = "s-override", planExternalId = "p-confirmed", name = "Snapshot Contratual Anterior", price = "77,25", durationMonths = "2", billingCycle = "monthly", startDate = start, endDate = end, chargeState = "Pending" },
            new { rowNumber = 4, externalId = "e-invalid-state", studentExternalId = "s-invalid", planExternalId = "p-confirmed", startDate = start, endDate = end, chargeState = "paid-ish" },
            new { rowNumber = 5, externalId = "e-empty-state", studentExternalId = "s-empty-state", planExternalId = "p-confirmed", startDate = start, endDate = end, chargeState = "" }
        ];
        var previewRows = await Preview(3, enrollmentRows);
        var inherited = previewRows.EnumerateArray().Single(row => row.GetProperty("rowNumber").GetInt32() == 2);
        Assert.True(inherited.GetProperty("isValid").GetBoolean());
        var inheritedValues = inherited.GetProperty("values");
        Assert.Equal("Plano Confirmado", inheritedValues.GetProperty("name").GetString());
        Assert.Equal("120,00", inheritedValues.GetProperty("price").GetString());
        Assert.Equal("3", inheritedValues.GetProperty("durationMonths").GetString());
        Assert.Equal("quarterly", inheritedValues.GetProperty("billingCycle").GetString());
        var overridden = previewRows.EnumerateArray().Single(row => row.GetProperty("rowNumber").GetInt32() == 3).GetProperty("values");
        Assert.Equal("Snapshot Contratual Anterior", overridden.GetProperty("name").GetString());
        Assert.Equal("77,25", overridden.GetProperty("price").GetString());
        Assert.Equal("2", overridden.GetProperty("durationMonths").GetString());
        Assert.Equal("monthly", overridden.GetProperty("billingCycle").GetString());
        var invalidState = previewRows.EnumerateArray().Single(row => row.GetProperty("rowNumber").GetInt32() == 4);
        Assert.False(invalidState.GetProperty("isValid").GetBoolean());
        Assert.Contains(invalidState.GetProperty("errors").EnumerateArray().Select(error => error.GetString()),
            error => error!.Contains("Situação da cobrança inválida", StringComparison.Ordinal));

        await Confirm(3, enrollmentRows);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            var plan = db.Plans.Single(item => item.Name == "Plano Confirmado");
            Assert.Equal(120m, plan.Price);
            Assert.Equal(3, plan.DurationMonths);
            var enrollments = db.Enrollments.Include(item => item.Charge).ToList();
            var inheritedEnrollments = enrollments.Where(item => item.PlanName == "Plano Confirmado").ToList();
            Assert.Equal(2, inheritedEnrollments.Count);
            Assert.All(inheritedEnrollments, item =>
            {
                Assert.Equal(120m, item.PlanPrice);
                Assert.Equal(PlanBillingCycle.Quarterly, item.PlanBillingCycle);
                Assert.Equal(ChargeVerificationStatus.Unverified, item.Charge!.VerificationStatus);
                Assert.Equal(ChargeStatus.Pending, item.Charge.Status);
            });
            var inheritedEnrollment = inheritedEnrollments[0];
            var overriddenEnrollment = enrollments.Single(item => item.PlanName == "Snapshot Contratual Anterior");
            Assert.Equal(77.25m, overriddenEnrollment.PlanPrice);
            Assert.Equal(2, overriddenEnrollment.PlanDurationMonths);
            Assert.Equal(PlanBillingCycle.Monthly, overriddenEnrollment.PlanBillingCycle);
            Assert.Equal(ChargeStatus.Pending, overriddenEnrollment.Charge!.Status);
            Assert.Equal(77.25m, overriddenEnrollment.Charge.Amount);
            Assert.Equal(3, enrollments.Count);
            Assert.Empty(db.FinancialAuditLogs.Where(log => log.Action == FinancialAuditAction.PaymentConfirmed));
        });
    }

    [Fact]
    public async Task MigrationEnrollment_RepreviewsSameBatchAfterPlansAreConfirmedBeforeRelease_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var created = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Homologação Migração Universal sintética",
            sourceNamespace = "sistema fictício"
        });
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<Guid> Confirm(int entityType, object[] rows)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType, rows });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
            var decisions = preview.GetProperty("rows").EnumerateArray().Select(row =>
            {
                Assert.True(row.GetProperty("isValid").GetBoolean());
                return new { rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew" };
            }).ToArray();
            var batchId = preview.GetProperty("batchId").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions })).StatusCode);
            return batchId;
        }

        await Confirm(1,
        [
            new { rowNumber = 2, externalId = "ALU-001", name = "Aluno sintético 001" },
            new { rowNumber = 3, externalId = "ALU-002", name = "Aluno sintético 002" },
            new { rowNumber = 4, externalId = "ALU-003", name = "Aluno sintético 003" }
        ]);

        var start = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1).ToString("yyyy-MM-dd");
        object[] enrollmentRows =
        [
            new { rowNumber = 2, externalId = "MAT-001", studentExternalId = "ALU-001", planExternalId = "PLA-001", startDate = start, endDate = end, chargeState = "Unverified" },
            new { rowNumber = 3, externalId = "MAT-002", studentExternalId = "ALU-002", planExternalId = "PLA-002", startDate = start, endDate = end, chargeState = "Pending" },
            new { rowNumber = 4, externalId = "MAT-003", studentExternalId = "ALU-003", planExternalId = "PLA-001", startDate = start, endDate = end, chargeState = "Paid" }
        ];

        var stalePreviewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 3,
            rows = enrollmentRows
        });
        var stalePreview = await stalePreviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var enrollmentBatchId = stalePreview.GetProperty("batchId").GetGuid();
        Assert.All(stalePreview.GetProperty("rows").EnumerateArray(), row => Assert.False(row.GetProperty("isValid").GetBoolean()));

        await Confirm(2,
        [
            new { rowNumber = 2, externalId = "PLA-001", name = "Plano sintético mensal", price = "120,00", durationMonths = "1", billingCycle = "Monthly" },
            new { rowNumber = 3, externalId = "PLA-002", name = "Plano sintético trimestral", price = "330,00", durationMonths = "3", billingCycle = "Quarterly" }
        ]);

        var refreshedResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 3,
            rows = enrollmentRows
        });
        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);
        var refreshed = await refreshedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(enrollmentBatchId, refreshed.GetProperty("batchId").GetGuid());
        Assert.True(refreshed.GetProperty("alreadyStaged").GetBoolean());
        var refreshedRows = refreshed.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, refreshedRows.Count);
        Assert.All(refreshedRows, row => Assert.True(row.GetProperty("isValid").GetBoolean()));
        Assert.Equal("Plano sintético mensal", refreshedRows[0].GetProperty("values").GetProperty("name").GetString());
        Assert.Equal("120,00", refreshedRows[0].GetProperty("values").GetProperty("price").GetString());
        Assert.Equal("Plano sintético trimestral", refreshedRows[1].GetProperty("values").GetProperty("name").GetString());
        Assert.Equal("330,00", refreshedRows[1].GetProperty("values").GetProperty("price").GetString());
        factory.WithDb(db =>
        {
            var batch = db.MigrationBatches.Single(item => item.Id == enrollmentBatchId);
            var payload = JsonDocument.Parse(batch.PayloadJson).RootElement;
            Assert.Equal("Plano sintético mensal", payload[0].GetProperty("name").GetString());
            Assert.Equal("120,00", payload[0].GetProperty("price").GetString());
            Assert.Equal("Plano sintético trimestral", payload[1].GetProperty("name").GetString());
            Assert.Equal("330,00", payload[1].GetProperty("price").GetString());
            Assert.Equal(MigrationBatchStatus.Previewed, batch.Status);
            Assert.Empty(db.Plans.Where(plan => plan.Name.StartsWith("Plano sintético")));
            Assert.Empty(db.MigrationExternalReferences.Where(reference =>
                reference.ExternalId == "PLA-001" || reference.ExternalId == "PLA-002"));
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
        });
    }

    [Fact]
    public async Task MigrationEnrollment_RejectsUnknownChargeStateAndDoesNotResolveForeignGymPlan_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var ownerSessionResponse = await client.PostAsJsonAsync("/api/migrations", new { name = "Plano de outra academia", sourceNamespace = "shared-source" });
        var ownerSession = (await ownerSessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var planPreview = await client.PostAsJsonAsync($"/api/migrations/{ownerSession}/preview", new
        {
            entityType = 2,
            rows = new[] { new { rowNumber = 2, externalId = "shared-plan", name = "Plano Privado", price = "90,00", durationMonths = "1", billingCycle = "monthly" } }
        });
        var planBody = await planPreview.Content.ReadFromJsonAsync<JsonElement>();
        var planBatch = planBody.GetProperty("batchId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/migrations/{ownerSession}/batches/{planBatch}/confirm",
            new { fingerprint = planBody.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{ownerSession}/release", new { })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        var foreignSessionResponse = await client.PostAsJsonAsync("/api/migrations", new { name = "Sem plano local", sourceNamespace = "shared-source" });
        var foreignSession = (await foreignSessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var enrollmentPreview = await client.PostAsJsonAsync($"/api/migrations/{foreignSession}/preview", new
        {
            entityType = 3,
            rows = new[] { new { rowNumber = 2, externalId = "foreign-enrollment", studentExternalId = "foreign-student", planExternalId = "shared-plan", startDate = "2026-01-01", endDate = "2026-02-01", chargeState = "mystery" } }
        });
        var enrollmentBody = await enrollmentPreview.Content.ReadFromJsonAsync<JsonElement>();
        var row = enrollmentBody.GetProperty("rows")[0];
        Assert.False(row.GetProperty("isValid").GetBoolean());
        var errors = row.GetProperty("errors").EnumerateArray().Select(error => error.GetString()).ToArray();
        Assert.Contains(errors, error => error!.Contains("Plano externo não localizado", StringComparison.Ordinal));
        Assert.Contains(errors, error => error!.Contains("Situação da cobrança inválida", StringComparison.Ordinal));
        Assert.Null(row.GetProperty("values").GetProperty("price").GetString());
        factory.WithDb(db => Assert.Single(db.Plans.Where(plan => plan.GymId == factory.EnabledGym && plan.Name == "Plano Privado")));
    }

    [PostgresFact]
    public Task MigrationSession_ConcurrentReleaseWritesOnlyOneSyntheticStudent_PostgreSql() =>
        CheckConcurrentMigrationRelease();

    [PostgresFact]
    public async Task MigrationBatch_DiscardConflictsWithConcurrentConfirmAndRelease_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        async Task<(Guid SessionId, Guid BatchId)> Stage(string name, string externalId)
        {
            var created = await client.PostAsJsonAsync("/api/migrations", new
            {
                name, sourceNamespace = "discard-race-source"
            });
            var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var staged = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
            {
                entityType = 1,
                rows = new[] { new { rowNumber = 2, externalId, name = "Aluno sintético em corrida" } }
            });
            var batchId = (await staged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("batchId").GetGuid();
            return (sessionId, batchId);
        }

        var (confirmSession, confirmBatch) = await Stage("Corrida confirmação", "confirm-race-student");
        var confirmation = client.PostAsJsonAsync(
            $"/api/migrations/{confirmSession}/batches/{confirmBatch}/confirm",
            new { fingerprint = await CurrentBatchFingerprint(client, confirmSession, confirmBatch),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } });
        var discard = client.PostAsJsonAsync(
            $"/api/migrations/{confirmSession}/batches/{confirmBatch}/discard", new { });
        var confirmRace = await Task.WhenAll(confirmation, discard);
        Assert.Contains(confirmRace, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Contains(confirmRace, response => response.StatusCode == HttpStatusCode.Conflict);

        var (releaseSession, releaseBatch) = await Stage("Corrida liberação", "release-race-student");
        var releaseRace = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/migrations/{releaseSession}/batches/{releaseBatch}/discard", new { }),
            client.PostAsJsonAsync($"/api/migrations/{releaseSession}/release", new { }));
        Assert.Contains(releaseRace[0].StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });
        Assert.Equal(HttpStatusCode.Conflict, releaseRace[1].StatusCode);
        var releaseDetails = await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{releaseSession}");
        var finalBatchStatus = releaseDetails.GetProperty("batches")[0].GetProperty("status").GetString();
        Assert.Equal(releaseRace[0].StatusCode == HttpStatusCode.NoContent ? "Discarded" : "Previewed", finalBatchStatus);
        Assert.Equal(1, releaseDetails.GetProperty("status").GetInt32());
        factory.WithDb(db => Assert.Empty(db.Students.Where(student =>
            student.Name == "Aluno sintético em corrida")));
    }

    private static async Task CheckConcurrentMigrationRelease()
    {
        await using var factory = new TestApi(usePostgres: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var created = await client.PostAsJsonAsync("/api/migrations", new { name = "Concorrência sintética", sourceNamespace = "concurrent-source" });
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var staged = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "student-1", name = "Aluno sintético concorrente" } }
        });
        var batch = await staged.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = batch.GetProperty("batchId").GetGuid();
        var decisions = new[] { new { rowNumber = 2, action = "ImportNew" } };
        await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
        { fingerprint = await CurrentBatchFingerprint(client, sessionId, batchId), decisions });

        var releases = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { }),
            client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { }));
        Assert.All(releases, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(releases, response => response.StatusCode == HttpStatusCode.OK);
        factory.WithDb(db => Assert.Equal(1, db.Students.Count(student => student.Name == "Aluno sintético concorrente")));
    }

    public sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")))
                Skip = "Requires an explicitly verified disposable loopback PostgreSQL cluster.";
        }
    }
}
