using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task MigrationSession_ConfirmationRejectsStalePreviewFingerprint_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "preview-version");
        var response = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "stu-fingerprint", name = "Aluno revisão" } }
        });
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        var fingerprint = preview.GetProperty("fingerprint").GetString();
        Assert.False(string.IsNullOrWhiteSpace(fingerprint));

        var stale = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{batchId}/confirm", new
        {
            fingerprint = "stale-review",
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Empty(db.Students.Where(student => student.Name == "Aluno revisão"));
            Assert.Equal(MigrationBatchStatus.Previewed, db.MigrationBatches.Single(batch => batch.Id == batchId).Status);
        });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{batchId}/confirm", new
            {
                fingerprint,
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
            })).StatusCode);
    }

    [Fact]
    public async Task MigrationSession_ReviewFingerprintIgnoresJsonStorageFormatting_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "jsonb-review-fingerprint");
        var response = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        {
            entityType = 1,
            rows = new[] { new { rowNumber = 2, externalId = "jsonb-review", name = "Fingerprint sintético" } }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        var fingerprint = preview.GetProperty("fingerprint").GetString();

        factory.WithDb(db =>
        {
            var batch = db.MigrationBatches.Single(item => item.Id == batchId);
            var indented = new JsonSerializerOptions { WriteIndented = true };
            batch.PayloadJson = JsonSerializer.Serialize(JsonDocument.Parse(batch.PayloadJson).RootElement, indented);
            batch.PreviewJson = JsonSerializer.Serialize(JsonDocument.Parse(batch.PreviewJson).RootElement, indented);
        });

        var confirm = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{batchId}/confirm", new
        {
            fingerprint,
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.True(confirm.StatusCode == HttpStatusCode.NoContent,
            $"A revisão JSON equivalente deve continuar válida após normalização do armazenamento. Resposta: {await confirm.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task MigrationSession_ExistingEquivalentPlanRequiresExplicitLinkAndPreservesTerms_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var existingId = Guid.NewGuid();
        factory.WithDb(db => db.Plans.Add(new Plan
        {
            Id = existingId, GymId = factory.EnabledGym, Name = "Plano Gold", Price = 75m,
            DurationMonths = 1, BillingCycle = PlanBillingCycle.Quarterly, IsActive = true
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "duplicate-plan-existing");
        var staged = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        {
            entityType = 2,
            rows = new[] { new { rowNumber = 2, externalId = "gold-import", name = "plano GOLD", price = "120,00", durationMonths = "12", billingCycle = "monthly" } }
        });
        var preview = await staged.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        Assert.True(preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        Assert.Contains(preview.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray(), item =>
            item.GetProperty("targetId").GetGuid() == existingId);

        var unsafeConfirm = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{batchId}/confirm", new
        {
            fingerprint = preview.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, unsafeConfirm.StatusCode);
        var safeConfirm = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{batchId}/confirm", new
        {
            fingerprint = preview.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "LinkExisting", targetId = existingId } }
        });
        Assert.Equal(HttpStatusCode.NoContent, safeConfirm.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            var plan = db.Plans.Single(item => item.Id == existingId);
            Assert.Equal(75m, plan.Price);
            Assert.Equal(1, plan.DurationMonths);
            Assert.Equal(PlanBillingCycle.Quarterly, plan.BillingCycle);
            Assert.Single(db.Plans.Where(item => item.GymId == factory.EnabledGym));
        });
    }

    [PostgresFact]
    public async Task MigrationSession_PlanNameCollisionUsesCitextSemantics_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var existingId = Guid.NewGuid();
        factory.WithDb(db => db.Plans.Add(new Plan
        {
            Id = existingId, GymId = factory.EnabledGym, Name = "Plano Citext", Price = 120m,
            DurationMonths = 3, BillingCycle = PlanBillingCycle.Quarterly, IsActive = true
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "citext-plan-check");
        var response = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        {
            entityType = 2,
            rows = new[] { new { rowNumber = 2, externalId = "new-citext", name = "plANO cITEXT", price = "330,00", durationMonths = "12", billingCycle = "monthly" } }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(preview.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray(), candidate =>
            candidate.GetProperty("targetId").GetGuid() == existingId);
        var result = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{preview.GetProperty("batchId").GetGuid()}/confirm", new
        {
            fingerprint = preview.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.True(result.StatusCode == HttpStatusCode.BadRequest,
            $"ImportNew precisa ser rejeitado como decisão inválida: {await result.Content.ReadAsStringAsync()}");
        factory.WithDb(db => Assert.Single(db.Plans.Where(plan => plan.GymId == factory.EnabledGym)));
    }

    [PostgresFact]
    public async Task MigrationSession_RevalidatesPlanNameCreatedAfterPreviewBeforeConfirmation_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "plan-created-after-preview");
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        {
            entityType = 2,
            rows = new[] { new { rowNumber = 2, externalId = "race-plan", name = "Plano Concorrente", price = "50,00", durationMonths = "1", billingCycle = "monthly" } }
        });
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        factory.WithDb(db => db.Plans.Add(new Plan
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "plano concorrente", Price = 80m,
            DurationMonths = 3, BillingCycle = PlanBillingCycle.Quarterly, IsActive = true
        }));

        var confirmation = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{batchId}/confirm", new
        {
            fingerprint = preview.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
        });
        Assert.Equal(HttpStatusCode.Conflict, confirmation.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Single(db.Plans.Where(plan => plan.GymId == factory.EnabledGym));
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
        });
    }

    [PostgresFact]
    public async Task MigrationSession_ValidatesNumericTenTwoBoundaryAgainstPostgreSqlSchema_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "numeric-boundary-postgres");
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        {
            entityType = 2,
            rows = new[]
            {
                new { rowNumber = 2, externalId = "max-price", name = "Preço limite PG", price = "99999999,99", durationMonths = "1", billingCycle = "monthly" },
                new { rowNumber = 3, externalId = "over-price", name = "Preço acima PG", price = "100000000,00", durationMonths = "1", billingCycle = "monthly" }
            }
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        Assert.False(preview.GetProperty("rows")[1].GetProperty("isValid").GetBoolean());
        var batchId = preview.GetProperty("batchId").GetGuid();
        var confirm = await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{batchId}/confirm", new
            {
                fingerprint = preview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "Ignore" } }
            });
        Assert.True(confirm.StatusCode == HttpStatusCode.NoContent,
            $"Confirmação do limite numeric(10,2) falhou: {await confirm.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })).StatusCode);
        factory.WithDb(db => Assert.Equal(99_999_999.99m,
            db.Plans.Single(plan => plan.GymId == factory.EnabledGym).Price));
    }

    [PostgresFact]
    public async Task MigrationSession_ConcurrentReleasesCannotCreateCitextPlanNameConflict_PostgreSql()
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        async Task<Guid> PrepareSession(string source, string externalId, string planName)
        {
            var session = await CreateMigrationSession(client, source);
            var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
            {
                entityType = 2,
                rows = new[] { new { rowNumber = 2, externalId, name = planName, price = "50,00", durationMonths = "1", billingCycle = "monthly" } }
            });
            var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
            var batchId = preview.GetProperty("batchId").GetGuid();
            var confirm = await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{batchId}/confirm", new
            {
                fingerprint = preview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } }
            });
            Assert.True(confirm.StatusCode == HttpStatusCode.NoContent,
                $"Confirmação do plano preparado para liberação falhou: {await confirm.Content.ReadAsStringAsync()}");
            return session;
        }

        var first = await PrepareSession("parallel-plan-a", "plan-a", "Plano Corrida Citext");
        var second = await PrepareSession("parallel-plan-b", "plan-b", "plano corrida citext");
        var releases = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/migrations/{first}/release", new { }),
            client.PostAsJsonAsync($"/api/migrations/{second}/release", new { }));
        Assert.Single(releases, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(releases, response => response.StatusCode == HttpStatusCode.Conflict);
        factory.WithDb(db =>
        {
            Assert.Single(db.Plans.Where(plan => plan.GymId == factory.EnabledGym));
            Assert.Single(db.MigrationExternalReferences.Where(reference => reference.EntityType == MigrationEntityType.Plans));
        });
    }

    [Fact]
    public async Task MigrationSession_DuplicateProposedPlansAcrossBatchesCanBeDiscardedAndReleaseRechecksDatabase_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "duplicate-plan-batches");

        async Task<(Guid BatchId, JsonElement Preview)> PreviewPlan(string externalId, string name)
        {
            var response = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
            {
                entityType = 2,
                rows = new[] { new { rowNumber = 2, externalId, name, price = "90,00", durationMonths = "3", billingCycle = "quarterly" } }
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (body.GetProperty("batchId").GetGuid(), body);
        }

        var first = await PreviewPlan("plan-first", "Plano Trio");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{first.BatchId}/confirm",
            new { fingerprint = first.Preview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);

        var second = await PreviewPlan("plan-second", "plano trio");
        Assert.False(second.Preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        Assert.Contains(second.Preview.GetProperty("rows")[0].GetProperty("errors").EnumerateArray(), error =>
            error.GetString()!.Contains("outro lote", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{second.BatchId}/confirm",
            new { fingerprint = second.Preview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{second.BatchId}/discard", new { })).StatusCode);

        var releasePlan = await PreviewPlan("plan-release-race", "Plano criado depois da confirmação");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{releasePlan.BatchId}/confirm",
            new { fingerprint = releasePlan.Preview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" } } })).StatusCode);
        var concurrentPlanId = Guid.NewGuid();
        factory.WithDb(db => db.Plans.Add(new Plan
        {
            Id = concurrentPlanId, GymId = factory.EnabledGym, Name = "plano CRIADO DEPOIS DA CONFIRMAÇÃO",
            Price = 1m, DurationMonths = 1, BillingCycle = PlanBillingCycle.Monthly, IsActive = true
        }));
        var release = await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        factory.WithDb(db =>
        {
            Assert.False(db.MigrationExternalReferences.Any(reference => reference.GymId == factory.EnabledGym &&
                reference.SourceNamespace == "duplicate-plan-batches" && reference.EntityType == MigrationEntityType.Plans));
            Assert.Equal(2, db.MigrationBatches.Count(batch => batch.MigrationSessionId == session &&
                batch.Status == MigrationBatchStatus.Confirmed));
        });

        var reviewResponse = await client.GetAsync(
            $"/api/migrations/{session}/batches/{releasePlan.BatchId}/review");
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var review = await reviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(review.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray(), candidate =>
            candidate.GetProperty("targetId").GetGuid() == concurrentPlanId);
        var resolution = new
        {
            fingerprint = review.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "LinkExisting", targetId = concurrentPlanId } }
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{releasePlan.BatchId}/review", resolution)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{releasePlan.BatchId}/confirm", resolution)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })).StatusCode);
        factory.WithDb(db => Assert.Equal(concurrentPlanId,
            db.MigrationExternalReferences.Single(reference => reference.SourceNamespace == "duplicate-plan-batches" &&
                reference.EntityType == MigrationEntityType.Plans && reference.ExternalId == "plan-release-race").TargetEntityId));

        var duplicateSession = await CreateMigrationSession(client, "duplicate-plan-same-batch");
        var duplicatePreviewResponse = await client.PostAsJsonAsync($"/api/migrations/{duplicateSession}/preview", new
        {
            entityType = 2,
            rows = new[]
            {
                new { rowNumber = 2, externalId = "same-a", name = "Plano Mesmo Lote", price = "90,00", durationMonths = "3", billingCycle = "quarterly" },
                new { rowNumber = 3, externalId = "same-b", name = "plano mesmo lote", price = "130,00", durationMonths = "12", billingCycle = "monthly" }
            }
        });
        var duplicatePreview = await duplicatePreviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var duplicateBatch = duplicatePreview.GetProperty("batchId").GetGuid();
        var bothNew = await client.PostAsJsonAsync($"/api/migrations/{duplicateSession}/batches/{duplicateBatch}/confirm", new
        {
            fingerprint = duplicatePreview.GetProperty("fingerprint").GetString(),
            decisions = new[] { new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "ImportNew" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, bothNew.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{duplicateSession}/batches/{duplicateBatch}/confirm", new
            {
                fingerprint = duplicatePreview.GetProperty("fingerprint").GetString(),
                decisions = new[] { new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "Ignore" } }
            })).StatusCode);
    }

    [Fact]
    public async Task MigrationSession_ValidatesPostgresExternalIdAndNumericLimitsBeforeConfirmation_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "schema-limits");
        var rows = new[]
        {
            new { rowNumber = 2, externalId = new string('a', 200), name = "Preço máximo", price = "99999999,99", durationMonths = "1", billingCycle = "monthly" },
            new { rowNumber = 3, externalId = new string('b', 201), name = "ID longo", price = "10,00", durationMonths = "1", billingCycle = "monthly" },
            new { rowNumber = 4, externalId = "price-overflow", name = "Preço excedido", price = "100000000,00", durationMonths = "1", billingCycle = "monthly" }
        };
        var response = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new { entityType = 2, rows });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        Assert.True(preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        Assert.False(preview.GetProperty("rows")[1].GetProperty("isValid").GetBoolean());
        Assert.False(preview.GetProperty("rows")[2].GetProperty("isValid").GetBoolean());
        Assert.Contains(preview.GetProperty("rows")[1].GetProperty("errors").EnumerateArray(), error =>
            error.GetString()!.Contains("200 caracteres"));
        Assert.Contains(preview.GetProperty("rows")[2].GetProperty("errors").EnumerateArray(), error =>
            error.GetString()!.Contains("numeric(10,2)"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{batchId}/confirm", new
            {
                fingerprint = preview.GetProperty("fingerprint").GetString(),
                decisions = new[]
                {
                    new { rowNumber = 2, action = "ImportNew" },
                    new { rowNumber = 3, action = "Ignore" },
                    new { rowNumber = 4, action = "Ignore" }
                }
            })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            var plan = db.Plans.Single(item => item.Name == "Preço máximo");
            Assert.Equal(99_999_999.99m, plan.Price);
            Assert.Single(db.Plans.Where(item => item.GymId == factory.EnabledGym));
        });
    }

    [Fact]
    public async Task StudentImport_ConfirmsOfficialFiveThousandRowLimit_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var rows = Enumerable.Range(1, 5_000).Select(index => new
        {
            rowNumber = index + 1, name = $"Importação sintética {index:00000}",
            email = (string?)null, phone = (string?)null, birthDate = (string?)null
        }).ToArray();
        var previewResponse = await client.PostAsJsonAsync("/api/students/imports/preview", new { rows });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        Assert.Equal(5_000, preview.GetProperty("rows").GetArrayLength());
        var decisions = rows.Select(row => new { rowNumber = row.rowNumber, action = "Import" }).ToArray();
        var confirmResponse = await client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", new
        {
            newStudentsActive = true, decisions
        });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal(5_000, (await confirmResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("results").GetArrayLength());
        factory.WithDb(db => Assert.Equal(5_000, db.Students.Count(student =>
            student.GymId == factory.EnabledGym && student.Name.StartsWith("Importação sintética "))));

        var excessiveDecisions = Enumerable.Range(1, 5_001)
            .Select(index => new { rowNumber = index, action = "Import" }).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", new
            { newStudentsActive = true, decisions = excessiveDecisions })).StatusCode);
    }

    [Fact]
    public async Task StudentImport_ValidatesTextColumnLimitsBeforeConfirmation_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var validEmail = new string('a', 64) + "@" + new string('b', 63) + "." +
            new string('c', 63) + "." + new string('d', 7);
        var invalidEmail = validEmail + "e";
        Assert.Equal(200, validEmail.Length);
        Assert.Equal(201, invalidEmail.Length);
        var previewResponse = await client.PostAsJsonAsync("/api/students/imports/preview", new
        {
            rows = new[]
            {
                new { rowNumber = 2, name = new string('N', 150), email = validEmail, phone = new string('1', 20), birthDate = (string?)null },
                new { rowNumber = 3, name = new string('N', 151), email = invalidEmail, phone = new string('1', 21), birthDate = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        Assert.True(preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean());
        var invalid = preview.GetProperty("rows")[1];
        Assert.False(invalid.GetProperty("isValid").GetBoolean());
        Assert.Contains(invalid.GetProperty("errors").EnumerateArray(), error => error.GetString()!.Contains("Nome excede 150"));
        Assert.Contains(invalid.GetProperty("errors").EnumerateArray(), error => error.GetString()!.Contains("E-mail excede 200"));
        Assert.Contains(invalid.GetProperty("errors").EnumerateArray(), error => error.GetString()!.Contains("Telefone excede 20"));
        var confirmed = await client.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", new
        {
            newStudentsActive = true,
            decisions = new[] { new { rowNumber = 2, action = "Import" } }
        });
        Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UniversalMigration_ConfirmsOfficialFiveThousandRowLimit_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var session = await CreateMigrationSession(client, "universal-5000");
        var rows = Enumerable.Range(1, 5_000).Select(index => new
        {
            rowNumber = index + 1, externalId = $"student-{index:00000}", name = $"Aluno universal {index:00000}"
        }).ToArray();
        var previewResponse = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new
        { entityType = 1, rows });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        Assert.Equal(5_000, preview.GetProperty("rows").GetArrayLength());
        var decisions = rows.Select(row => new { rowNumber = row.rowNumber, action = "ImportNew" }).ToArray();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/migrations/{session}/batches/{batchId}/confirm", new
            { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions })).StatusCode);
        factory.WithDb(db => Assert.Equal(5_000, db.MigrationBatches.Single(batch => batch.Id == batchId)
            .PayloadJson.Count(character => character == '{')));
    }

    private static async Task<Guid> CreateMigrationSession(HttpClient client, string source)
    {
        var response = await client.PostAsJsonAsync("/api/migrations", new
        { name = "Integridade de revisão", sourceNamespace = source });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
