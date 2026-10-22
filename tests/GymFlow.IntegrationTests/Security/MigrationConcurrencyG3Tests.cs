using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    private static async Task<string> CurrentBatchFingerprint(HttpClient client, Guid sessionId, Guid batchId)
    {
        var response = await client.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = await response.Content.ReadFromJsonAsync<JsonElement>();
        return review.GetProperty("fingerprint").GetString()!;
    }

    [Fact]
    public Task MigrationPreview_RacingReleaseCannotCreateBatchAfterSessionRelease_Sqlite() =>
        CheckPreviewRacingRelease(false);

    [PostgresFact]
    public Task MigrationPreview_RacingReleaseCannotCreateBatchAfterSessionRelease_PostgreSql() =>
        CheckPreviewRacingRelease(true);

    private async Task CheckPreviewRacingRelease(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var sessionResponse = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Prévia versus liberação",
            sourceNamespace = "g3-preview-release"
        });
        var sessionId = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var firstPayload = new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "student-before-release", name = "Aluno sintético" } }
        };
        var firstPreview = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", firstPayload);
        var first = await firstPreview.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = first.GetProperty("batchId").GetGuid();
        var confirmed = await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/confirm",
            new
            {
                fingerprint = first.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
            });
        Assert.True(confirmed.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação inicial falhou: {await confirmed.Content.ReadAsStringAsync()}");

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.BeforeNextTransaction = async _ =>
        {
            entered.TrySetResult();
            await resume.Task;
        };
        var latePreviewTask = client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "student-too-late", name = "Aluno tardio" } }
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        resume.TrySetResult();
        var latePreview = await latePreviewTask;
        Assert.Equal(HttpStatusCode.Conflict, latePreview.StatusCode);

        var session = await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{sessionId}");
        Assert.Equal((int)MigrationSessionStatus.Released, session.GetProperty("status").GetInt32());
        Assert.Single(session.GetProperty("batches").EnumerateArray());
        factory.WithDb(db => Assert.Equal(new[] { "Aluno sintético" },
            db.Students.Select(student => student.Name).ToArray()));
    }

    [Fact]
    public Task MigrationPreview_RacingDiscardCannotResurrectDiscardedBatch_Sqlite() =>
        CheckPreviewRacingDiscard(false);

    [PostgresFact]
    public Task MigrationPreview_RacingDiscardCannotResurrectDiscardedBatch_PostgreSql() =>
        CheckPreviewRacingDiscard(true);

    private async Task CheckPreviewRacingDiscard(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var sessionResponse = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Prévia versus descarte",
            sourceNamespace = "g3-preview-discard"
        });
        var sessionId = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var payload = new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "student-discard-race", name = "Aluno descartado" } }
        };
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", payload);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.BeforeNextTransaction = async _ =>
        {
            entered.TrySetResult();
            await resume.Task;
        };
        var repeatedPreviewTask = client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", payload);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var discard = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{batchId}/discard", new { });
        Assert.Equal(HttpStatusCode.NoContent, discard.StatusCode);
        resume.TrySetResult();
        var repeatedPreview = await repeatedPreviewTask;
        Assert.Equal(HttpStatusCode.Conflict, repeatedPreview.StatusCode);

        var session = await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{sessionId}");
        var batch = Assert.Single(session.GetProperty("batches").EnumerateArray());
        Assert.Equal("Discarded", batch.GetProperty("status").GetString());
        Assert.Equal(1, batch.GetProperty("rowCount").GetInt32()); // Count remains visible; payload and values are discarded.
        factory.WithDb(db => Assert.Empty(db.Students));
    }

    [Fact]
    public Task MigrationConfirmation_RejectsDecisionsFromPlanPreviewRevisionThatChanged_Sqlite() =>
        CheckStalePlanReview(false);

    [PostgresFact]
    public Task MigrationConfirmation_RejectsDecisionsFromPlanPreviewRevisionThatChanged_PostgreSql() =>
        CheckStalePlanReview(true);

    private async Task CheckStalePlanReview(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessionResponse = await client.PostAsJsonAsync("/api/migrations", new
        {
            name = "Revisão de plano obsoleta",
            sourceNamespace = "g3-stale-review"
        });
        var sessionId = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 2,
            rows = new[]
            {
                new { rowNumber = 2, externalId = "plan-g3-stale", name = "Plano criado após prévia", price = "120,00", durationMonths = "3", billingCycle = "quarterly" }
            }
        });
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        var oldRevision = preview.GetProperty("fingerprint").GetString();
        var existingPlanId = Guid.NewGuid();
        factory.WithDb(db => db.Plans.Add(new GymFlow.Domain.Entities.Plan
        {
            Id = existingPlanId, GymId = factory.EnabledGym, Name = "Plano criado após prévia",
            Price = 80m, DurationMonths = 1, BillingCycle = PlanBillingCycle.Monthly, IsActive = true
        }));

        var staleConfirm = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
        {
            fingerprint = oldRevision,
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.Equal(HttpStatusCode.Conflict, staleConfirm.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationBatchStatus.Previewed, db.MigrationBatches.Single(batch => batch.Id == batchId).Status);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId));
        });

        var refreshedResponse = await client.GetAsync($"/api/migrations/{sessionId}/batches/{batchId}/review");
        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);
        var refreshed = await refreshedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(oldRevision, refreshed.GetProperty("fingerprint").GetString());
        Assert.Empty(refreshed.GetProperty("decisions").EnumerateArray());
        Assert.Contains(refreshed.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray(), candidate =>
            candidate.GetProperty("targetId").GetGuid() == existingPlanId);

        var resolved = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
        {
            fingerprint = refreshed.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "LinkExisting", targetId = existingPlanId } }
        });
        Assert.True(resolved.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação após revisão atualizada falhou: {await resolved.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db => Assert.Equal(existingPlanId,
            db.MigrationExternalReferences.Single(reference => reference.MigrationSessionId == sessionId).TargetEntityId));
    }

    [PostgresFact]
    public async Task MigrationRelease_ConcurrentOperationalPaymentIsPreservedAndMigrationRollsBack_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "g3-payment-release-race";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "g3-payment-enrollment", "g3-payment");
        Guid chargeId = Guid.Empty;
        factory.WithDb(db => chargeId = db.Charges.Single(charge => charge.EnrollmentId == target.EnrollmentId).Id);
        var sessionId = await CreateMigrationSession(client, "Pagamento operacional versus liberação", source);
        var preview = await PreviewFinancialRow(client, sessionId, "g3-financial-payment", target.StudentExternalId,
            target.EnrollmentExternalId, "Paid");
        Assert.False(preview.Row.GetProperty("isValid").GetBoolean());
        Assert.True(preview.Row.GetProperty("requiresFinancialResolution").GetBoolean());
        Assert.Contains("Conflito de situação financeira", string.Join(" ", preview.Row.GetProperty("errors")
            .EnumerateArray().Select(error => error.GetString())), StringComparison.OrdinalIgnoreCase);
        var confirmation = await ConfirmFinancialRow(client, sessionId, preview.BatchId,
            preview.Row.GetProperty("rowNumber").GetInt32(), "UseFinancialFile");
        Assert.True(confirmation.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação financeira resolvida falhou: {await confirmation.Content.ReadAsStringAsync()}");

        HttpStatusCode? competingPaymentStatus = null;
        string? competingPaymentBody = null;
        var competingPaymentReached = false;
        factory.BeforeNextAsyncSave = async _ =>
        {
            competingPaymentReached = true;
            var payment = await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", new
            {
                paidAmount = 120m,
                discountAmount = 0m,
                paymentMethod = (int)PaymentMethod.Pix,
                paidAt = DateTimeOffset.UtcNow
            });
            competingPaymentStatus = payment.StatusCode;
            competingPaymentBody = await payment.Content.ReadAsStringAsync();
        };
        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        Assert.True(competingPaymentReached, "O callback da confirmação de pagamento não foi alcançado durante a liberação.");
        Assert.True(competingPaymentStatus == HttpStatusCode.NoContent,
            $"A confirmação operacional concorrente retornou {competingPaymentStatus}: {competingPaymentBody}");
        factory.WithDb(db =>
        {
            var charge = db.Charges.Include(item => item.Enrollment).SingleOrDefault(item => item.Id == chargeId)
                ?? throw new Xunit.Sdk.XunitException($"A cobrança concorrente {chargeId} não foi encontrada após o rollback da liberação.");
            Assert.Equal(ChargeStatus.Paid, charge.Status);
            Assert.Equal(PaymentOrigin.Avelri, charge.PaymentOrigin);
            Assert.Equal(120m, charge.PaidAmount);
            Assert.NotNull(charge.PaidAt);
            var enrollment = charge.Enrollment
                ?? throw new Xunit.Sdk.XunitException($"A matrícula da cobrança {chargeId} não foi carregada para verificar o rollback.");
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
            var paymentAudit = db.FinancialAuditLogs.SingleOrDefault(item => item.EntityId == chargeId &&
                item.Action == FinancialAuditAction.PaymentConfirmed)
                ?? throw new Xunit.Sdk.XunitException($"A auditoria PaymentConfirmed da cobrança {chargeId} não foi preservada.");
            Assert.Empty(db.MigrationExternalReferences.Where(item => item.MigrationSessionId == sessionId &&
                item.EntityType == MigrationEntityType.Charges));
            var migrationSession = db.MigrationSessions.SingleOrDefault(item => item.Id == sessionId)
                ?? throw new Xunit.Sdk.XunitException($"A sessão {sessionId} não foi encontrada após o rollback da liberação.");
            Assert.Equal(MigrationSessionStatus.Preparing, migrationSession.Status);
        });
    }

    [PostgresFact]
    public async Task MigrationRelease_ConcurrentEnrollmentCancellationIsPreservedAndMigrationRollsBack_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "g3-cancel-release-race";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "g3-cancel-enrollment", "g3-cancel");
        Guid chargeId = Guid.Empty;
        factory.WithDb(db => chargeId = db.Charges.Single(charge => charge.EnrollmentId == target.EnrollmentId).Id);
        var sessionId = await CreateMigrationSession(client, "Cancelamento versus liberação", source);
        var preview = await PreviewFinancialRow(client, sessionId, "g3-financial-cancel", target.StudentExternalId,
            target.EnrollmentExternalId, "Paid");
        Assert.False(preview.Row.GetProperty("isValid").GetBoolean());
        Assert.True(preview.Row.GetProperty("requiresFinancialResolution").GetBoolean());
        Assert.Contains("Conflito de situação financeira", string.Join(" ", preview.Row.GetProperty("errors")
            .EnumerateArray().Select(error => error.GetString())), StringComparison.OrdinalIgnoreCase);
        var confirmation = await ConfirmFinancialRow(client, sessionId, preview.BatchId,
            preview.Row.GetProperty("rowNumber").GetInt32(), "UseFinancialFile");
        Assert.True(confirmation.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação financeira resolvida falhou: {await confirmation.Content.ReadAsStringAsync()}");

        HttpStatusCode? competingCancellationStatus = null;
        string? competingCancellationBody = null;
        var competingCancellationReached = false;
        factory.BeforeNextAsyncSave = async _ =>
        {
            competingCancellationReached = true;
            var cancellation = await client.PatchAsync($"/api/enrollments/{target.EnrollmentId}/cancel", null);
            competingCancellationStatus = cancellation.StatusCode;
            competingCancellationBody = await cancellation.Content.ReadAsStringAsync();
        };
        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        Assert.True(competingCancellationReached, "O callback de cancelamento não foi alcançado durante a liberação.");
        Assert.True(competingCancellationStatus == HttpStatusCode.NoContent,
            $"O cancelamento concorrente retornou {competingCancellationStatus}: {competingCancellationBody}");
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Single(item => item.Id == target.EnrollmentId);
            Assert.Equal(EnrollmentStatus.Cancelled, enrollment.Status);
            Assert.Equal(ChargeStatus.Cancelled, db.Charges.Single(item => item.Id == chargeId).Status);
            Assert.Single(db.FinancialAuditLogs.Where(item => item.EntityId == chargeId &&
                item.Action == FinancialAuditAction.ChargeCancelled));
            Assert.Empty(db.MigrationExternalReferences.Where(item => item.MigrationSessionId == sessionId &&
                item.EntityType == MigrationEntityType.Charges));
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(item => item.Id == sessionId).Status);
        });
    }
}
