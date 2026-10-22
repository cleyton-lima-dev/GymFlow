using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task MigrationFinancialBatch_RejectsRealAvelriReceiptWithoutChangingFinancialDetails_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p0-real-payment";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-real-paid");
        var paidAt = DateTime.UtcNow.AddDays(-2);
        var auditCountBefore = 0;
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            enrollment.Charge!.Status = ChargeStatus.Paid;
            enrollment.Charge.Origin = ChargeOrigin.Avelri;
            enrollment.Charge.PaymentOrigin = PaymentOrigin.Avelri;
            enrollment.Charge.PaidAmount = 117.50m;
            enrollment.Charge.PaidAt = paidAt;
            enrollment.Charge.PaymentMethod = PaymentMethod.Pix;
            enrollment.Charge.DiscountAmount = 2.50m;
            auditCountBefore = db.FinancialAuditLogs.Count();
        });

        var sessionId = await CreateMigrationSession(client, "Tentativa de sobrescrita", source);
        var preview = await PreviewFinancialRow(client, sessionId, "new-charge-id", target.StudentExternalId,
            target.EnrollmentExternalId, "Paid");
        Assert.False(preview.Row.GetProperty("isValid").GetBoolean());
        Assert.Contains("cobrança contém estado operacional", string.Join(" ", preview.Row.GetProperty("errors")
            .EnumerateArray().Select(error => error.GetString())), StringComparison.OrdinalIgnoreCase);
        var confirm = await ConfirmFinancialRow(client, sessionId, preview.BatchId, preview.Row.GetProperty("rowNumber").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);

        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            var charge = enrollment.Charge!;
            Assert.Equal(EnrollmentStatus.PendingPayment, enrollment.Status);
            Assert.Equal(ChargeStatus.Paid, charge.Status);
            Assert.Equal(ChargeOrigin.Avelri, charge.Origin);
            Assert.Equal(PaymentOrigin.Avelri, charge.PaymentOrigin);
            Assert.Equal(117.50m, charge.PaidAmount);
            Assert.Equal(paidAt, charge.PaidAt);
            Assert.Equal(PaymentMethod.Pix, charge.PaymentMethod);
            Assert.Equal(2.50m, charge.DiscountAmount);
            Assert.Equal(auditCountBefore, db.FinancialAuditLogs.Count());
        });
    }

    [Fact]
    public async Task MigrationFinancialBatch_DoesNotReactivateCancelledEnrollment_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p0-cancelled";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-cancelled");
        var cancellationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            enrollment.Status = EnrollmentStatus.Cancelled;
            enrollment.CancellationDate = cancellationDate;
        });

        var sessionId = await CreateMigrationSession(client, "Tentativa de reativação", source);
        var preview = await PreviewFinancialRow(client, sessionId, "new-charge-id", target.StudentExternalId,
            target.EnrollmentExternalId, "Paid");
        Assert.False(preview.Row.GetProperty("isValid").GetBoolean());
        Assert.Contains("matrícula está cancelada", string.Join(" ", preview.Row.GetProperty("errors")
            .EnumerateArray().Select(error => error.GetString())), StringComparison.OrdinalIgnoreCase);
        var confirm = await ConfirmFinancialRow(client, sessionId, preview.BatchId, preview.Row.GetProperty("rowNumber").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);

        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            Assert.Equal(EnrollmentStatus.Cancelled, enrollment.Status);
            Assert.Equal(cancellationDate, enrollment.CancellationDate);
            Assert.Equal(ChargeStatus.Pending, enrollment.Charge!.Status);
            Assert.Equal(PaymentOrigin.None, enrollment.Charge.PaymentOrigin);
            Assert.Null(enrollment.Charge.PaidAmount);
            Assert.Null(enrollment.Charge.PaidAt);
        });
    }

    [Fact]
    public async Task MigrationFinancialBatch_RevalidatesRealPaymentAtReleaseAndRollsBack_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p0-release-race";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-release-race");
        var sessionId = await CreateMigrationSession(client, "Pagamento após prévia", source);
        var preview = await PreviewFinancialRow(client, sessionId, "new-charge-id", target.StudentExternalId,
            target.EnrollmentExternalId, "Pending");
        Assert.True(preview.Row.GetProperty("isValid").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmFinancialRow(client, sessionId, preview.BatchId,
            preview.Row.GetProperty("rowNumber").GetInt32())).StatusCode);

        var paidAt = DateTime.UtcNow.AddMinutes(-1);
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            enrollment.Charge!.Status = ChargeStatus.Paid;
            enrollment.Charge.Origin = ChargeOrigin.Avelri;
            enrollment.Charge.PaymentOrigin = PaymentOrigin.Avelri;
            enrollment.Charge.PaidAmount = 120m;
            enrollment.Charge.PaidAt = paidAt;
            enrollment.Charge.PaymentMethod = PaymentMethod.Card;
            enrollment.Charge.DiscountAmount = 4m;
        });

        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        Assert.Contains("cobrança contém estado operacional", await release.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            Assert.Equal(EnrollmentStatus.PendingPayment, enrollment.Status);
            Assert.Equal(ChargeStatus.Paid, enrollment.Charge!.Status);
            Assert.Equal(ChargeOrigin.Avelri, enrollment.Charge.Origin);
            Assert.Equal(PaymentOrigin.Avelri, enrollment.Charge.PaymentOrigin);
            Assert.Equal(120m, enrollment.Charge.PaidAmount);
            Assert.Equal(paidAt, enrollment.Charge.PaidAt);
            Assert.Equal(PaymentMethod.Card, enrollment.Charge.PaymentMethod);
            Assert.Equal(4m, enrollment.Charge.DiscountAmount);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.GymId == factory.EnabledGym &&
                reference.SourceNamespace == source && reference.EntityType == MigrationEntityType.Charges &&
                reference.ExternalId == "new-charge-id"));
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(item => item.Id == sessionId).Status);
        });
    }

    [Fact]
    public async Task MigrationFinancialBatch_ConfirmationIgnoresJsonbObjectPropertyOrdering_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-jsonb-order";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-jsonb-order");
        var sessionId = await CreateMigrationSession(client, "Ordem JSON da prévia", source);
        var preview = await PreviewFinancialRow(client, sessionId, "charge-jsonb-order", target.StudentExternalId,
            target.EnrollmentExternalId, "Pending");
        Assert.True(preview.Row.GetProperty("isValid").GetBoolean());
        var fingerprint = await CurrentBatchFingerprint(client, sessionId, preview.BatchId);

        // PostgreSQL jsonb returns object keys in its normalized order. Simulate
        // that representation for the preview's Values dictionary in SQLite.
        factory.WithDb(db =>
        {
            var batch = db.MigrationBatches.Single(item => item.Id == preview.BatchId);
            var rows = JsonNode.Parse(batch.PreviewJson)!.AsArray();
            var row = rows[0]!.AsObject();
            var values = row["values"]!.AsObject();
            var normalizedValues = new JsonObject();
            foreach (var property in values.OrderBy(property => property.Key, StringComparer.Ordinal))
                normalizedValues.Add(property.Key, property.Value?.DeepClone());
            row["values"] = normalizedValues;
            batch.PreviewJson = rows.ToJsonString();
        });

        var confirm = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{preview.BatchId}/confirm", new
        {
            fingerprint,
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.True(confirm.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação sem mudança semântica retornou {confirm.StatusCode}: {await confirm.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db => Assert.Equal(ChargeStatus.Pending,
            db.Charges.Single(item => item.EnrollmentId == target.EnrollmentId).Status));
    }

    [Fact]
    public async Task MigrationFinancialBatch_RejectsOperationalChangeAfterPreviewAndPreservesReceipt_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-change-after-preview";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-change-after-preview");
        Guid chargeId = Guid.Empty;
        factory.WithDb(db => chargeId = db.Charges.Single(item => item.EnrollmentId == target.EnrollmentId).Id);
        var sessionId = await CreateMigrationSession(client, "Mudança após prévia", source);
        var preview = await PreviewFinancialRow(client, sessionId, "charge-change-after-preview", target.StudentExternalId,
            target.EnrollmentExternalId, "Pending");
        Assert.True(preview.Row.GetProperty("isValid").GetBoolean());
        var fingerprint = await CurrentBatchFingerprint(client, sessionId, preview.BatchId);
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var payment = await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", new
        {
            paidAmount = 120m, discountAmount = 0m, paymentMethod = (int)PaymentMethod.Pix, paidAt
        });
        Assert.Equal(HttpStatusCode.NoContent, payment.StatusCode);

        var confirm = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{preview.BatchId}/confirm", new
        {
            fingerprint,
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Contains("prévia financeira mudou", await confirm.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        factory.WithDb(db =>
        {
            var charge = db.Charges.Single(item => item.Id == chargeId);
            Assert.Equal(ChargeStatus.Paid, charge.Status);
            Assert.Equal(PaymentOrigin.Avelri, charge.PaymentOrigin);
            Assert.Equal(120m, charge.PaidAmount);
            Assert.Equal(paidAt.UtcDateTime, charge.PaidAt);
            Assert.Single(db.FinancialAuditLogs.Where(item => item.EntityId == chargeId &&
                item.Action == FinancialAuditAction.PaymentConfirmed));
            var batch = db.MigrationBatches.Single(item => item.Id == preview.BatchId);
            Assert.Equal(MigrationBatchStatus.Previewed, batch.Status);
            Assert.Equal("[]", batch.DecisionsJson);
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(item => item.Id == sessionId).Status);
        });
    }

    [Fact]
    public async Task MigrationFinancialBatch_StillUpdatesUnpaidMigrationCharge_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p0-legitimate";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-legitimate");
        var sessionId = await CreateMigrationSession(client, "Atualização financeira legítima", source);
        var preview = await PreviewFinancialRow(client, sessionId, "new-charge-id", target.StudentExternalId,
            target.EnrollmentExternalId, "Unverified");
        Assert.False(preview.Row.GetProperty("isValid").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmFinancialRow(client, sessionId, preview.BatchId,
            preview.Row.GetProperty("rowNumber").GetInt32(), "UseFinancialFile")).StatusCode);
        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            Assert.Equal(EnrollmentStatus.PendingPayment, enrollment.Status);
            Assert.Equal(ChargeStatus.Pending, enrollment.Charge!.Status);
            Assert.Equal(ChargeOrigin.Migration, enrollment.Charge.Origin);
            Assert.Equal(PaymentOrigin.None, enrollment.Charge.PaymentOrigin);
            Assert.Equal(ChargeVerificationStatus.Unverified, enrollment.Charge.VerificationStatus);
            Assert.Null(enrollment.Charge.PaidAmount);
            Assert.Null(enrollment.Charge.PaidAt);
        });
    }

    [Fact]
    public async Task MigrationFinancialBatch_RejectsSameBatchTargetDuplicatesAndAllowsExplicitIgnore_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p1-same-batch";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-p1-same");
        var sessionId = await CreateMigrationSession(client, "Duplicidade mesmo lote", source);
        object[] rows =
        [
            new { rowNumber = 2, externalId = "finance-alias-a", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Paid" },
            new { rowNumber = 3, externalId = "finance-alias-b", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" },
            new { rowNumber = 4, externalId = "finance-alias-c", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" }
        ];
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows });
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        Assert.All(preview.GetProperty("rows").EnumerateArray(), row =>
        {
            Assert.False(row.GetProperty("isValid").GetBoolean());
            Assert.Contains("mesma matrícula/cobrança", string.Join(" ", row.GetProperty("errors").EnumerateArray()
                .Select(error => error.GetString())), StringComparison.OrdinalIgnoreCase);
        });

        var repeated = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows });
        var repeatedPreview = await repeated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(repeatedPreview.GetProperty("alreadyStaged").GetBoolean());
        Assert.Equal(batchId, repeatedPreview.GetProperty("batchId").GetGuid());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>($"/api/migrations/{sessionId}"))
            .GetProperty("batches").GetArrayLength());

        var decisions = new[]
        {
            new { rowNumber = 2, action = "Ignore", financialStateResolution = (string?)null },
            new { rowNumber = 3, action = "ImportNew", financialStateResolution = (string?)null },
            new { rowNumber = 4, action = "Ignore", financialStateResolution = (string?)null }
        };
        var confirmation = await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
            { fingerprint = await CurrentBatchFingerprint(client, sessionId, batchId), decisions });
        Assert.True(confirmation.StatusCode == HttpStatusCode.NoContent,
            $"{confirmation.StatusCode}: {await confirmation.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            Assert.Equal(ChargeStatus.Pending, enrollment.Charge!.Status);
            Assert.Single(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId &&
                reference.EntityType == MigrationEntityType.Charges));
        });
    }

    [Fact]
    public async Task MigrationFinancialBatch_RejectsDuplicateAcrossConfirmedBatches_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p1-cross-batch";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-p1-cross");
        var sessionId = await CreateMigrationSession(client, "Duplicidade entre lotes", source);
        var first = await PreviewFinancialRow(client, sessionId, "finance-first", target.StudentExternalId,
            target.EnrollmentExternalId, "Pending");
        Assert.True(first.Row.GetProperty("isValid").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmFinancialRow(client, sessionId, first.BatchId,
            first.Row.GetProperty("rowNumber").GetInt32())).StatusCode);

        var second = await PreviewFinancialRow(client, sessionId, "finance-second", target.StudentExternalId,
            target.EnrollmentExternalId, "Paid");
        Assert.False(second.Row.GetProperty("isValid").GetBoolean());
        Assert.Contains("já confirmada nesta sessão", string.Join(" ", second.Row.GetProperty("errors")
            .EnumerateArray().Select(error => error.GetString())), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmFinancialRow(client, sessionId, second.BatchId,
            second.Row.GetProperty("rowNumber").GetInt32(), ignore: true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db => Assert.Equal(ChargeStatus.Pending, db.Charges.Single(item => item.EnrollmentId == target.EnrollmentId).Status));
    }

    [Fact]
    public async Task MigrationFinancialBatch_ResolvesEnrollmentAndChargeAliasesToSameTarget_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p1-aliases";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-p1-alias");
        Guid chargeId = Guid.Empty;
        Guid sourceSessionId = Guid.Empty;
        factory.WithDb(db =>
        {
            chargeId = db.Charges.Single(item => item.EnrollmentId == target.EnrollmentId).Id;
            sourceSessionId = db.MigrationSessions.Single(item => item.GymId == factory.EnabledGym && item.SourceNamespace == source).Id;
            db.MigrationExternalReferences.Add(new MigrationExternalReference
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, MigrationSessionId = sourceSessionId,
                SourceNamespace = source, EntityType = MigrationEntityType.Charges,
                ExternalId = "charge-alias-p1", TargetEntityId = chargeId, CreatedAt = DateTime.UtcNow
            });
        });
        var sessionId = await CreateMigrationSession(client, "Aliases para mesma cobrança", source);
        var rows = new object[]
        {
            new { rowNumber = 2, externalId = "finance-by-enrollment", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" },
            new { rowNumber = 3, externalId = "finance-by-charge", studentExternalId = target.StudentExternalId,
                chargeExternalId = "charge-alias-p1", chargeState = "Pending" }
        };
        var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows });
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(preview.GetProperty("rows").EnumerateArray(), row => Assert.False(row.GetProperty("isValid").GetBoolean()));
        Assert.All(preview.GetProperty("rows").EnumerateArray(), row => Assert.Contains("mesma matrícula/cobrança",
            string.Join(" ", row.GetProperty("errors").EnumerateArray().Select(error => error.GetString())),
            StringComparison.OrdinalIgnoreCase));
        factory.WithDb(db => Assert.Equal(ChargeStatus.Pending,
            db.Charges.Single(item => item.EnrollmentId == target.EnrollmentId).Status));
    }

    [Fact]
    public async Task MigrationFinancialBatch_ConflictsAreScopedToGymAndSession_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var otherAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p1-isolation";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-p1-isolation");
        var ownSession = await CreateMigrationSession(client, "Academia original", source);
        var ownPreview = await PreviewFinancialRow(client, ownSession, "finance-own", target.StudentExternalId,
            target.EnrollmentExternalId, "Pending");
        Assert.True(ownPreview.Row.GetProperty("isValid").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmFinancialRow(client, ownSession, ownPreview.BatchId,
            ownPreview.Row.GetProperty("rowNumber").GetInt32())).StatusCode);
        var sameSessionDuplicate = await PreviewFinancialRow(client, ownSession, "finance-own-alias", target.StudentExternalId,
            target.EnrollmentExternalId, "Pending");
        Assert.False(sameSessionDuplicate.Row.GetProperty("isValid").GetBoolean());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var otherSession = await CreateMigrationSession(client, "Outra sessão", source);
        var otherSessionPreview = await PreviewFinancialRow(client, otherSession, "finance-other-session",
            target.StudentExternalId, target.EnrollmentExternalId, "Pending");
        Assert.True(otherSessionPreview.Row.GetProperty("isValid").GetBoolean());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var foreignResponse = await client.PostAsJsonAsync($"/api/migrations/{ownSession}/preview", new
        {
            entityType = 4,
            rows = new[] { new { rowNumber = 2, externalId = "finance-foreign", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" } }
        });
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
    }

    [Fact]
    public async Task MigrationFinancialBatch_RevalidatesDuplicateTargetsAtReleaseWithoutPartialChanges_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "financial-p1-release-revalidation";
        var firstTarget = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-race-a", "race-a");
        var secondTarget = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-race-b", "race-b");
        var sessionId = await CreateMigrationSession(client, "Vínculos alterados após revisão", source);
        var rows = new object[]
        {
            new { rowNumber = 2, externalId = "finance-race-a", studentExternalId = firstTarget.StudentExternalId,
                enrollmentExternalId = firstTarget.EnrollmentExternalId, chargeState = "Pending" },
            new { rowNumber = 3, externalId = "finance-race-b", studentExternalId = secondTarget.StudentExternalId,
                enrollmentExternalId = secondTarget.EnrollmentExternalId, chargeState = "Pending" }
        };
        var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = 4, rows });
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(preview.GetProperty("rows").EnumerateArray(), row => Assert.True(row.GetProperty("isValid").GetBoolean()));
        var batchId = preview.GetProperty("batchId").GetGuid();
        var decisions = preview.GetProperty("rows").EnumerateArray().Select(row => new
        { rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew" }).ToArray();
        var confirmation = await client.PostAsJsonAsync(
            $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
            { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions });
        Assert.True(confirmation.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação financeira antes da revalidação falhou: {await confirmation.Content.ReadAsStringAsync()}");

        factory.WithDb(db =>
        {
            var reference = db.MigrationExternalReferences.Single(item => item.GymId == factory.EnabledGym &&
                item.SourceNamespace == source && item.EntityType == MigrationEntityType.Enrollments &&
                item.ExternalId == secondTarget.EnrollmentExternalId);
            reference.TargetEntityId = firstTarget.EnrollmentId;
        });
        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        Assert.Contains("várias linhas confirmadas apontam para a mesma matrícula/cobrança",
            await release.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(item => item.Id == sessionId).Status);
            Assert.Empty(db.MigrationExternalReferences.Where(item => item.MigrationSessionId == sessionId &&
                item.EntityType == MigrationEntityType.Charges));
            Assert.Equal(ChargeStatus.Pending, db.Charges.Single(item => item.EnrollmentId == firstTarget.EnrollmentId).Status);
            Assert.Equal(ChargeStatus.Pending, db.Charges.Single(item => item.EnrollmentId == secondTarget.EnrollmentId).Status);
        });
    }

    private static async Task<(Guid StudentId, Guid EnrollmentId, string StudentExternalId, string EnrollmentExternalId)>
        CreateReleasedMigratedEnrollment(TestApi factory, HttpClient client, string source, string enrollmentExternalId,
            string suffix = "p0")
    {
        var sessionId = await CreateMigrationSession(client, "Matrícula base sintética", source);
        async Task Confirm(int type, object[] rows)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType = type, rows });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
            var batchId = preview.GetProperty("batchId").GetGuid();
            var decisions = preview.GetProperty("rows").EnumerateArray().Select(row => new
            { rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew" }).ToArray();
            var confirmation = await client.PostAsJsonAsync(
                $"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
                { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions });
            var confirmationBody = await confirmation.Content.ReadAsStringAsync();
            Assert.True(confirmation.StatusCode == HttpStatusCode.NoContent,
                $"Confirmação do lote {type} retornou {(int)confirmation.StatusCode} ({confirmation.StatusCode}). " +
                $"BatchId={batchId}; fingerprint={preview.GetProperty("fingerprint").GetString()}; " +
                $"resposta={confirmationBody}");
        }

        var studentExternalId = $"student-{suffix}";
        var planExternalId = $"plan-{suffix}";
        await Confirm(1, [new { rowNumber = 2, externalId = studentExternalId, name = $"Aluno {suffix} sintético" }]);
        await Confirm(2, [new { rowNumber = 2, externalId = planExternalId, name = $"Plano {suffix} sintético", price = "120.00", durationMonths = "12", billingCycle = "monthly" }]);
        var start = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(12).ToString("yyyy-MM-dd");
        await Confirm(3, [new { rowNumber = 2, externalId = enrollmentExternalId, studentExternalId,
            planExternalId, startDate = start, endDate = end, chargeState = "Pending" }]);
        await Confirm(4, [new { rowNumber = 2, externalId = $"charge-{suffix}-base", studentExternalId,
            enrollmentExternalId, chargeState = "Pending" }]);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        Guid studentId = Guid.Empty;
        Guid enrollmentId = Guid.Empty;
        factory.WithDb(db =>
        {
            studentId = db.Students.Single(item => item.Name == $"Aluno {suffix} sintético").Id;
            enrollmentId = db.Enrollments.Single(item => item.StudentId == studentId).Id;
        });
        return (studentId, enrollmentId, studentExternalId, enrollmentExternalId);
    }

    private static async Task<Guid> CreateMigrationSession(HttpClient client, string name, string source)
    {
        var response = await client.PostAsJsonAsync("/api/migrations", new { name, sourceNamespace = source });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(JsonElement Row, Guid BatchId)> PreviewFinancialRow(HttpClient client, Guid sessionId,
        string chargeExternalId, string studentExternalId, string enrollmentExternalId, string state)
    {
        var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new
        {
            entityType = 4,
            rows = new[] { new { rowNumber = 2, externalId = chargeExternalId, studentExternalId, enrollmentExternalId, chargeState = state } }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (preview.GetProperty("rows")[0], preview.GetProperty("batchId").GetGuid());
    }

    private static async Task<HttpResponseMessage> ConfirmFinancialRow(HttpClient client, Guid sessionId, Guid batchId, int rowNumber,
        string? resolution = null, bool ignore = false) =>
        await client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{batchId}/confirm", new
        {
            fingerprint = await CurrentBatchFingerprint(client, sessionId, batchId),
            decisions = new[] { new { rowNumber, action = ignore ? "Ignore" : "ImportNew", financialStateResolution = resolution } }
        });
}
