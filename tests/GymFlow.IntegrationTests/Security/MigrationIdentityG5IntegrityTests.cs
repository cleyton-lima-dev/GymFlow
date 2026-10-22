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
    public async Task MigrationIdentityG5_NewMatchingStudentInvalidatesPreviewBeforeConfirmation_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessionId = await CreateMigrationSession(client, "Contato criado após prévia", "g5-stale-student-preview");
        const string email = "g5-stale-preview@example.invalid";
        var preview = await G5IntegrityPreview(client, sessionId, MigrationEntityType.Students,
            [new { rowNumber = 2, externalId = "student-stale", name = "Aluno na origem", email }]);
        Assert.Empty(preview.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray());
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Aluno existente criado depois", Email = email
        }));

        var stale = await G5IntegrityConfirm(client, sessionId, preview,
            [new { rowNumber = 2, action = "ImportNew" }]);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationBatchStatus.Previewed,
                db.MigrationBatches.Single(batch => batch.Id == preview.GetProperty("batchId").GetGuid()).Status);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId));
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym));
        });

        var review = await client.GetFromJsonAsync<JsonElement>(
            $"/api/migrations/{sessionId}/batches/{preview.GetProperty("batchId").GetGuid()}/review");
        Assert.NotEqual(preview.GetProperty("fingerprint").GetString(), review.GetProperty("fingerprint").GetString());
        Assert.Contains(review.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray(), candidate =>
            candidate.GetProperty("targetId").ValueKind == JsonValueKind.String &&
            candidate.GetProperty("targetId").GetGuid() == studentId);
        Assert.Empty(review.GetProperty("decisions").EnumerateArray());
        var resolved = await G5IntegrityConfirm(client, sessionId, review,
            [new { rowNumber = 2, action = "LinkExisting", targetId = studentId }]);
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym));
            Assert.Equal(studentId, db.MigrationExternalReferences.Single(reference => reference.MigrationSessionId == sessionId).TargetEntityId);
        });
    }

    [Fact]
    public async Task MigrationIdentityG5_NewMatchingStudentAfterConfirmationBlocksReleaseAtomically_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessionId = await CreateMigrationSession(client, "Contato criado após confirmação", "g5-stale-student-release");
        const string phone = "11999887766";
        var preview = await G5IntegrityPreview(client, sessionId, MigrationEntityType.Students,
            [new { rowNumber = 2, externalId = "student-after-confirm", name = "Aluno para revisar", phone }]);
        Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, sessionId, preview,
            [new { rowNumber = 2, action = "ImportNew" }])).StatusCode);
        var existingId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = existingId, GymId = factory.EnabledGym, Name = "Cadastro operacional posterior", Phone = phone,
            IsActive = false, InactivationReason = StudentInactivationReason.Manual
        }));

        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(session => session.Id == sessionId).Status);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId));
            var student = Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym));
            Assert.Equal(existingId, student.Id);
            Assert.False(student.IsActive);
            Assert.Equal(StudentInactivationReason.Manual, student.InactivationReason);
            Assert.Null(student.UserId);
            Assert.False(student.AppAccessEnabled);
        });
    }

    [Fact]
    public async Task MigrationIdentityG5_AliasReleasedAfterPreviewInvalidatesStudentReview_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        const string email = "g5-alias-late@example.invalid";
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Dono do alias", Email = email
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "g5-alias-after-preview";
        var staleSession = await CreateMigrationSession(client, "Prévia anterior ao vínculo", source);
        var stalePreview = await G5IntegrityPreview(client, staleSession, MigrationEntityType.Students,
            [new { rowNumber = 2, externalId = "shared-external-student", name = "Registro sem contato" }]);
        Assert.Empty(stalePreview.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray());

        var linkingSession = await CreateMigrationSession(client, "Decisão explícita concorrente", source);
        var linkedPreview = await G5IntegrityPreview(client, linkingSession, MigrationEntityType.Students,
            [new { rowNumber = 2, externalId = "shared-external-student", name = "Dono do alias", email }]);
        Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, linkingSession, linkedPreview,
            [new { rowNumber = 2, action = "LinkExisting", targetId = studentId }])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/migrations/{linkingSession}/release", new { })).StatusCode);

        var staleConfirmation = await G5IntegrityConfirm(client, staleSession, stalePreview,
            [new { rowNumber = 2, action = "ImportNew" }]);
        Assert.Equal(HttpStatusCode.Conflict, staleConfirmation.StatusCode);
        var review = await client.GetFromJsonAsync<JsonElement>(
            $"/api/migrations/{staleSession}/batches/{stalePreview.GetProperty("batchId").GetGuid()}/review");
        Assert.NotEqual(stalePreview.GetProperty("fingerprint").GetString(), review.GetProperty("fingerprint").GetString());
        Assert.Contains(review.GetProperty("rows")[0].GetProperty("candidates").EnumerateArray(), candidate =>
            candidate.GetProperty("targetId").ValueKind == JsonValueKind.String &&
            candidate.GetProperty("targetId").GetGuid() == studentId);
        factory.WithDb(db =>
        {
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym));
            var reference = Assert.Single(db.MigrationExternalReferences.Where(reference => reference.GymId == factory.EnabledGym &&
                reference.SourceNamespace == source && reference.EntityType == MigrationEntityType.Students));
            Assert.Equal(studentId, reference.TargetEntityId);
            Assert.Equal(linkingSession, reference.MigrationSessionId);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == staleSession));
        });
    }

    [Fact]
    public async Task MigrationIdentityG5_IdenticalHistoricalEnrollmentsThroughStudentAliasesRollBackWholeRelease_Sqlite()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        const string email = "g5-history@example.invalid";
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Aluno com dois aliases", Email = email
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var sessionId = await CreateMigrationSession(client, "Matrículas históricas duplicadas", "g5-history-aliases");
        var students = await G5IntegrityPreview(client, sessionId, MigrationEntityType.Students,
        [
            new { rowNumber = 2, externalId = "history-student-a", name = "Primeiro alias", email },
            new { rowNumber = 3, externalId = "history-student-b", name = "Segundo alias", email },
            new { rowNumber = 4, externalId = "rollback-student", name = "Novo aluno deve sofrer rollback", email = (string?)null }
        ]);
        Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, sessionId, students,
        [
            new { rowNumber = 2, action = "LinkExisting", targetId = (Guid?)studentId },
            new { rowNumber = 3, action = "LinkExisting", targetId = (Guid?)studentId },
            new { rowNumber = 4, action = "ImportNew", targetId = (Guid?)null }
        ])).StatusCode);
        var plans = await G5IntegrityPreview(client, sessionId, MigrationEntityType.Plans,
            [new { rowNumber = 2, externalId = "history-plan", name = "Plano histórico", price = "120.00", durationMonths = "1", billingCycle = "monthly" }]);
        Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, sessionId, plans,
            [new { rowNumber = 2, action = "ImportNew" }])).StatusCode);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-2);
        var start = end.AddMonths(-1);
        var enrollments = await G5IntegrityPreview(client, sessionId, MigrationEntityType.Enrollments,
        [
            new { rowNumber = 2, externalId = "history-enrollment-a", studentExternalId = "history-student-a",
                planExternalId = "history-plan", startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd"), chargeState = "Paid" },
            new { rowNumber = 3, externalId = "history-enrollment-b", studentExternalId = "history-student-b",
                planExternalId = "history-plan", startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd"), chargeState = "Paid" }
        ]);
        Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, sessionId, enrollments,
            [new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "ImportNew" }])).StatusCode);

        var release = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(session => session.Id == sessionId).Status);
            Assert.Equal(studentId, Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym)).Id);
            Assert.Empty(db.Plans.Where(plan => plan.GymId == factory.EnabledGym));
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.Empty(db.FinancialAuditLogs);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId));
            Assert.All(db.MigrationBatches.Where(batch => batch.MigrationSessionId == sessionId), batch =>
            {
                Assert.Equal(MigrationBatchStatus.Confirmed, batch.Status);
                Assert.Null(batch.ReleasedAt);
                Assert.NotEqual("[]", batch.PayloadJson);
            });
        });

        var reviewResponse = await client.GetAsync(
            $"/api/migrations/{sessionId}/batches/{enrollments.GetProperty("batchId").GetGuid()}/review");
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var review = await reviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, sessionId, review,
            [new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "Ignore" }])).StatusCode);
        var recovered = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/release", new { });
        Assert.True(recovered.StatusCode == HttpStatusCode.OK,
            $"A revisão da matrícula duplicada deve permitir recuperar a sessão: {await recovered.Content.ReadAsStringAsync()}");
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationSessionStatus.Released, db.MigrationSessions.Single(session => session.Id == sessionId).Status);
            Assert.Equal(2, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym && student.Name == "Novo aluno deve sofrer rollback"));
            Assert.Single(db.Plans.Where(plan => plan.GymId == factory.EnabledGym));
            var enrollment = Assert.Single(db.Enrollments);
            Assert.Equal(studentId, enrollment.StudentId);
            Assert.Equal(EnrollmentStatus.Expired, enrollment.Status);
            var charge = Assert.Single(db.Charges);
            Assert.Equal(enrollment.Id, charge.EnrollmentId);
            Assert.Equal(PaymentOrigin.Migration, charge.PaymentOrigin);
            Assert.Null(charge.PaidAt);
            Assert.Null(charge.PaidAmount);
            Assert.Empty(db.FinancialAuditLogs);
            Assert.Single(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId &&
                reference.EntityType == MigrationEntityType.Enrollments && reference.ExternalId == "history-enrollment-a"));
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == sessionId &&
                reference.EntityType == MigrationEntityType.Enrollments && reference.ExternalId == "history-enrollment-b"));
        });
    }

    [PostgresFact]
    public Task MigrationIdentityG5_ConcurrentSessionsCannotDuplicateSameStudentAlias_PostgreSql() =>
        G5IntegrityCheckConcurrentAliasRelease(contradictoryTargets: false);

    [PostgresFact]
    public Task MigrationIdentityG5_ConcurrentSessionsCannotRedirectStudentAlias_PostgreSql() =>
        G5IntegrityCheckConcurrentAliasRelease(contradictoryTargets: true);

    private static async Task G5IntegrityCheckConcurrentAliasRelease(bool contradictoryTargets)
    {
        await using var factory = new TestApi(usePostgres: true, migrateSchema: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var firstStudentId = Guid.NewGuid();
        var secondStudentId = contradictoryTargets ? Guid.NewGuid() : firstStudentId;
        const string firstEmail = "g5-concurrent-a@example.invalid";
        var secondEmail = contradictoryTargets ? "g5-concurrent-b@example.invalid" : firstEmail;
        factory.WithDb(db =>
        {
            db.Students.Add(new Student { Id = firstStudentId, GymId = factory.EnabledGym, Name = "Aluno concorrente A", Email = firstEmail });
            if (contradictoryTargets)
                db.Students.Add(new Student { Id = secondStudentId, GymId = factory.EnabledGym, Name = "Aluno concorrente B", Email = secondEmail });
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        const string source = "g5-concurrent-alias-release";
        const string externalId = "student-shared-alias";
        async Task<Guid> Prepare(string name, string email, Guid targetId)
        {
            var sessionId = await CreateMigrationSession(client, name, source);
            var preview = await G5IntegrityPreview(client, sessionId, MigrationEntityType.Students,
                [new { rowNumber = 2, externalId, name, email }]);
            Assert.Equal(HttpStatusCode.NoContent, (await G5IntegrityConfirm(client, sessionId, preview,
                [new { rowNumber = 2, action = "LinkExisting", targetId }])).StatusCode);
            return sessionId;
        }

        var firstSession = await Prepare("Primeira decisão concorrente", firstEmail, firstStudentId);
        var secondSession = await Prepare("Segunda decisão concorrente", secondEmail, secondStudentId);
        // Hold the first release after its serializable read and before the alias INSERT.
        // The second release commits the same unique key, exercising a real PostgreSQL race.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.BeforeNextAsyncSave = async db =>
        {
            Assert.Contains(db.ChangeTracker.Entries<MigrationExternalReference>(), entry =>
                entry.State == EntityState.Added && entry.Entity.ExternalId == externalId);
            entered.TrySetResult();
            await resume.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };
        var firstReleaseTask = client.PostAsJsonAsync($"/api/migrations/{firstSession}/release", new { });
        HttpResponseMessage secondRelease;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            secondRelease = await client.PostAsJsonAsync($"/api/migrations/{secondSession}/release", new { });
        }
        finally
        {
            resume.TrySetResult();
        }
        var firstRelease = await firstReleaseTask;
        Assert.True(secondRelease.StatusCode == HttpStatusCode.OK,
            $"A segunda liberação deveria vencer a corrida: {await secondRelease.Content.ReadAsStringAsync()}");
        Assert.True(firstRelease.StatusCode == HttpStatusCode.Conflict,
            $"A primeira liberação deve retornar conflito controlado: {firstRelease.StatusCode}; {await firstRelease.Content.ReadAsStringAsync()}; {factory.CapturedExceptions}");
        factory.WithDb(db =>
        {
            var reference = Assert.Single(db.MigrationExternalReferences.Where(reference => reference.GymId == factory.EnabledGym &&
                reference.SourceNamespace == source && reference.EntityType == MigrationEntityType.Students && reference.ExternalId == externalId));
            Assert.Equal(secondStudentId, reference.TargetEntityId);
            Assert.Equal(secondSession, reference.MigrationSessionId);
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(session => session.Id == firstSession).Status);
            Assert.Equal(MigrationSessionStatus.Released, db.MigrationSessions.Single(session => session.Id == secondSession).Status);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == firstSession));
            Assert.Equal(contradictoryTargets ? 2 : 1, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
        });
        var repeated = await client.PostAsJsonAsync($"/api/migrations/{secondSession}/release", new { });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        factory.WithDb(db => Assert.Single(db.MigrationExternalReferences.Where(reference =>
            reference.GymId == factory.EnabledGym && reference.SourceNamespace == source && reference.ExternalId == externalId)));
    }

    private static async Task<JsonElement> G5IntegrityPreview(HttpClient client, Guid sessionId,
        MigrationEntityType entityType, object[] rows)
    {
        var response = await client.PostAsJsonAsync($"/api/migrations/{sessionId}/preview", new { entityType, rows });
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Prévia G5 falhou: {response.StatusCode}; {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> G5IntegrityConfirm(HttpClient client, Guid sessionId,
        JsonElement preview, object[] decisions) =>
        client.PostAsJsonAsync($"/api/migrations/{sessionId}/batches/{preview.GetProperty("batchId").GetGuid()}/confirm",
            new { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions });
}
