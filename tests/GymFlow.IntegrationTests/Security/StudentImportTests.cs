using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GymFlow.Api.Controllers;
using GymFlow.Application.DTOs.Students;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Metadata;
using Npgsql;
using Xunit.Abstractions;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    private readonly ITestOutputHelper _testOutput;

    public TenantEnrollmentAccessTests(ITestOutputHelper testOutput) => _testOutput = testOutput;

    [Fact]
    public void StudentImport_PreviewEndpointRetainsFiveMillionByteRequestLimit()
    {
        var action = typeof(StudentImportsController).GetMethod(
            nameof(StudentImportsController.Preview));
        Assert.NotNull(action);
        var requestLimit = action.GetCustomAttributes()
            .OfType<IRequestSizeLimitMetadata>()
            .SingleOrDefault();
        Assert.NotNull(requestLimit);
        Assert.Equal(5_000_000L, requestLimit.MaxRequestBodySize);
    }

    [Theory]
    [InlineData("name", "E-mail")]
    [InlineData("email", "Telefone")]
    [InlineData("phone", "E-mail")]
    [InlineData("archived", "E-mail")]
    public async Task StudentImport_ChangedDisplayedCandidateRequiresFreshPreview_Sqlite(
        string changedField,
        string expectedMatchBy)
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var candidate = new Student
        {
            Id = Guid.NewGuid(),
            GymId = factory.EnabledGym,
            Name = "Nome da prévia",
            Email = "candidate@example.test",
            Phone = "5511999990000",
            IsActive = true,
            AppAccessEnabled = false
        };
        factory.WithDb(db => db.Students.Add(candidate));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var row = changedField == "email"
            ? new StudentImportInputRow(2, "Novo aluno sintético", null, candidate.Phone, null)
            : changedField == "phone"
                ? new StudentImportInputRow(2, "Novo aluno sintético", candidate.Email, null, null)
                : new StudentImportInputRow(2, "Novo aluno sintético", candidate.Email, null, null);
        var previewResponse = await client.PostAsJsonAsync(
            "/api/students/imports/preview", new { rows = new[] { row } });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = preview.GetProperty("batchId").GetGuid();
        var oldCandidate = preview.GetProperty("rows")[0].GetProperty("candidates")[0];
        Assert.Equal(candidate.Id, oldCandidate.GetProperty("studentId").GetGuid());
        Assert.Equal(expectedMatchBy, oldCandidate.GetProperty("matchBy").GetString());

        factory.WithDb(db =>
        {
            var changed = db.Students.Single(student => student.Id == candidate.Id);
            switch (changedField)
            {
                case "name":
                    changed.Name = "Nome atualizado após a prévia";
                    break;
                case "email":
                    changed.Email = "updated@example.test";
                    break;
                case "phone":
                    changed.Phone = "5511999990001";
                    break;
                case "archived":
                    changed.ArchivedAt = DateTime.UtcNow.AddMinutes(-1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(changedField));
            }
        });

        var confirmation = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm",
            new
            {
                newStudentsActive = true,
                decisions = new[] { new { rowNumber = 2, action = "ImportAsNew" } }
            });
        Assert.Equal(HttpStatusCode.Conflict, confirmation.StatusCode);
        var conflict = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Gere uma nova prévia", conflict.GetProperty("message").GetString());
        var refreshedCandidate = conflict.GetProperty("rows")[0].GetProperty("candidates")[0];
        Assert.Equal(expectedMatchBy, refreshedCandidate.GetProperty("matchBy").GetString());
        switch (changedField)
        {
            case "name":
                Assert.Equal("Nome atualizado após a prévia", refreshedCandidate.GetProperty("name").GetString());
                break;
            case "email":
                Assert.Equal("updated@example.test", refreshedCandidate.GetProperty("email").GetString());
                break;
            case "phone":
                Assert.Equal("5511999990001", refreshedCandidate.GetProperty("phone").GetString());
                break;
            case "archived":
                Assert.True(refreshedCandidate.GetProperty("isArchived").GetBoolean());
                break;
        }

        factory.WithDb(db =>
        {
            Assert.Equal(1, db.Students.Count(student => student.GymId == factory.EnabledGym));
            var savedBatch = db.StudentImportBatches.Single(batch => batch.Id == batchId);
            Assert.Equal(0, savedBatch.State);
            Assert.NotNull(savedBatch.PayloadJson);
            Assert.NotNull(savedBatch.PreviewJson);
        });
    }

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
        var archivedCandidate = previewRows[1].GetProperty("candidates")[0];
        Assert.Equal(existing.Id, archivedCandidate.GetProperty("studentId").GetGuid());
        Assert.Equal(existing.Name, archivedCandidate.GetProperty("name").GetString());
        Assert.Equal(existing.Email, archivedCandidate.GetProperty("email").GetString());
        Assert.Equal("E-mail", archivedCandidate.GetProperty("matchBy").GetString());
        Assert.True(archivedCandidate.GetProperty("isArchived").GetBoolean());
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

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_StudentImportAllowsUnchangedReviewAndPreservesTenantIsolationAndIdempotency(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: false);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var otherAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        AssertActiveDisposablePostgresHarness();

        var unchangedCandidate = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Archived synthetic candidate",
            Email = "unchanged-review@example.invalid", IsActive = false,
            AppAccessEnabled = false, ArchivedAt = DateTime.UtcNow.AddDays(-2)
        };
        var foreignCandidate = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.OtherEnabledGym, Name = "Foreign synthetic candidate",
            Phone = "5511999912345", IsActive = true, AppAccessEnabled = false
        };
        factory.WithDb(db => db.Students.AddRange(unchangedCandidate, foreignCandidate));

        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var rows = new[]
        {
            new StudentImportInputRow(2, "Synthetic import as new", unchangedCandidate.Email, null, null),
            new StudentImportInputRow(3, "Foreign contact synthetic import", null, foreignCandidate.Phone, null)
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var previewResponse = await client.PostAsJsonAsync(
            "/api/students/imports/preview", new { rows }, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
        var batchId = preview.GetProperty("batchId").GetGuid();
        var previewRows = preview.GetProperty("rows").EnumerateArray().ToArray();
        var archivedDuplicate = previewRows[0].GetProperty("candidates")[0];
        Assert.Equal(unchangedCandidate.Id, archivedDuplicate.GetProperty("studentId").GetGuid());
        Assert.True(archivedDuplicate.GetProperty("isArchived").GetBoolean());
        Assert.Equal("E-mail", archivedDuplicate.GetProperty("matchBy").GetString());
        Assert.Empty(previewRows[1].GetProperty("candidates").EnumerateArray());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var foreignTenantAttempt = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm",
            new { newStudentsActive = true, decisions = Array.Empty<object>() }, timeout.Token);
        Assert.Equal(HttpStatusCode.NotFound, foreignTenantAttempt.StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var confirmationBody = new
        {
            newStudentsActive = true,
            decisions = new[]
            {
                new { rowNumber = 2, action = "ImportAsNew" },
                new { rowNumber = 3, action = "Import" }
            }
        };
        var confirmation = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var repeated = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.True((await repeated.Content.ReadFromJsonAsync<JsonElement>(timeout.Token))
            .GetProperty("alreadyCompleted").GetBoolean());

        factory.WithDb(db =>
        {
            Assert.Equal(3, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Single(db.Students.Where(student => student.GymId == factory.OtherEnabledGym));
            var unchanged = db.Students.Single(student => student.Id == unchangedCandidate.Id);
            Assert.NotNull(unchanged.ArchivedAt);
            Assert.False(unchanged.IsActive);
            var imports = db.Students.Where(student =>
                student.GymId == factory.EnabledGym &&
                (student.Name == "Synthetic import as new" || student.Name == "Foreign contact synthetic import"))
                .ToList();
            Assert.Equal(2, imports.Count);
            Assert.All(imports, student =>
            {
                Assert.Null(student.UserId);
                Assert.False(student.AppAccessEnabled);
            });
            Assert.Equal(2, db.StudentImportBatches.Single(batch => batch.Id == batchId).State);
        });
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_StudentImportRejectsChangedReviewSnapshotAtomically(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: false);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        AssertActiveDisposablePostgresHarness();

        var candidates = new[]
        {
            new Student
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Name before review",
                Email = "name-review@example.invalid", IsActive = true, AppAccessEnabled = false
            },
            new Student
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Email before review",
                Email = "email-before@example.invalid", Phone = "5511999910101",
                IsActive = true, AppAccessEnabled = false
            },
            new Student
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Phone before review",
                Email = "phone-review@example.invalid", Phone = "5511999910102",
                IsActive = true, AppAccessEnabled = false
            },
            new Student
            {
                Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Archive before review",
                Email = "archive-review@example.invalid", IsActive = true, AppAccessEnabled = false
            }
        };
        factory.WithDb(db => db.Students.AddRange(candidates));

        var rows = new[]
        {
            new StudentImportInputRow(10, "Name row synthetic", candidates[0].Email, null, null),
            new StudentImportInputRow(11, "Email row synthetic", null, candidates[1].Phone, null),
            new StudentImportInputRow(12, "Phone row synthetic", candidates[2].Email, null, null),
            new StudentImportInputRow(13, "Archive row synthetic", candidates[3].Email, null, null)
        };
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var previewResponse = await client.PostAsJsonAsync(
            "/api/students/imports/preview", new { rows }, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
        var batchId = preview.GetProperty("batchId").GetGuid();
        var expectedMatchBy = new[] { "E-mail", "Telefone", "E-mail", "E-mail" };
        var previewRows = preview.GetProperty("rows").EnumerateArray().ToArray();
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = previewRows[index].GetProperty("candidates")[0];
            Assert.Equal(candidates[index].Id, candidate.GetProperty("studentId").GetGuid());
            Assert.Equal(expectedMatchBy[index], candidate.GetProperty("matchBy").GetString());
        }

        factory.WithDb(db =>
        {
            db.Students.Single(student => student.Id == candidates[0].Id).Name = "Name after review";
            db.Students.Single(student => student.Id == candidates[1].Id).Email = "email-after@example.invalid";
            db.Students.Single(student => student.Id == candidates[2].Id).Phone = "5511999910999";
            db.Students.Single(student => student.Id == candidates[3].Id).ArchivedAt = DateTime.UtcNow.AddMinutes(-1);
        });

        var confirmationBody = new
        {
            newStudentsActive = true,
            decisions = new[]
            {
                new { rowNumber = 10, action = "ImportAsNew" },
                new { rowNumber = 11, action = "ImportAsNew" },
                new { rowNumber = 12, action = "ImportAsNew" },
                new { rowNumber = 13, action = "ImportAsNew" }
            }
        };
        var rejected = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var conflict = await rejected.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
        Assert.Contains("Gere uma nova prévia", conflict.GetProperty("message").GetString());
        var changedRows = conflict.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal("Name after review", changedRows[0].GetProperty("candidates")[0].GetProperty("name").GetString());
        Assert.Equal("email-after@example.invalid", changedRows[1].GetProperty("candidates")[0].GetProperty("email").GetString());
        Assert.Equal("5511999910999", changedRows[2].GetProperty("candidates")[0].GetProperty("phone").GetString());
        Assert.True(changedRows[3].GetProperty("candidates")[0].GetProperty("isArchived").GetBoolean());
        for (var index = 0; index < candidates.Length; index++)
        {
            Assert.Equal(candidates[index].Id,
                changedRows[index].GetProperty("candidates")[0].GetProperty("studentId").GetGuid());
            Assert.Equal(expectedMatchBy[index],
                changedRows[index].GetProperty("candidates")[0].GetProperty("matchBy").GetString());
        }

        factory.WithDb(db =>
        {
            Assert.Equal(4, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Empty(db.Students.Where(student => student.Name.EndsWith("row synthetic")));
            var savedBatch = db.StudentImportBatches.Single(batch => batch.Id == batchId);
            Assert.Equal(0, savedBatch.State);
            Assert.NotNull(savedBatch.PayloadJson);
            Assert.NotNull(savedBatch.PreviewJson);
        });

        var renewedPreviewResponse = await client.PostAsJsonAsync(
            "/api/students/imports/preview", new { rows }, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, renewedPreviewResponse.StatusCode);
        Assert.Equal(batchId,
            (await renewedPreviewResponse.Content.ReadFromJsonAsync<JsonElement>(timeout.Token))
                .GetProperty("batchId").GetGuid());
        var accepted = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var acceptedResult = await accepted.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
        Assert.Equal(4, acceptedResult.GetProperty("results").EnumerateArray().Count(
            result => result.GetProperty("status").GetString() == "Imported"));
        var idempotent = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, idempotent.StatusCode);
        Assert.True((await idempotent.Content.ReadFromJsonAsync<JsonElement>(timeout.Token))
            .GetProperty("alreadyCompleted").GetBoolean());

        factory.WithDb(db =>
        {
            Assert.Equal(8, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Equal(2, db.StudentImportBatches.Single(batch => batch.Id == batchId).State);
        });
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_StudentImportSerializableEditAndConcurrentConfirmationsAreAtomic(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: false);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        AssertActiveDisposablePostgresHarness();

        var candidate = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Concurrent candidate before",
            Email = "serializable-review@example.invalid", IsActive = true, AppAccessEnabled = false
        };
        var foreignCandidate = new Student
        {
            Id = Guid.NewGuid(), GymId = factory.OtherEnabledGym, Name = "Foreign-only candidate",
            Email = "foreign-only-review@example.invalid", IsActive = true, AppAccessEnabled = false
        };
        factory.WithDb(db => db.Students.AddRange(candidate, foreignCandidate));
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var reviewRows = new[]
        {
            new StudentImportInputRow(2, "Concurrent import synthetic", candidate.Email, null, null),
            new StudentImportInputRow(3, "Tenant-isolated import synthetic", foreignCandidate.Email, null, null)
        };
        using var previewResponse = await client.PostAsJsonAsync(
            "/api/students/imports/preview", new { rows = reviewRows }, timeout.Token);
        var previewObservation = await ReadStudentImportResponseOnceAsync(previewResponse, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, previewObservation.StatusCode);
        Assert.True(previewObservation.BatchId.HasValue);
        var batchId = previewObservation.BatchId!.Value;
        Assert.Equal(new[] { 1, 0 }, previewObservation.CandidateCounts);

        var competingEditCommitted = false;
        var competingEditSerializationConflict = false;
        var interleavingReached = false;
        factory.BeforeNextAsyncSave = async _ =>
        {
            interleavingReached = true;
            await using var competingScope = factory.Services.CreateAsyncScope();
            var competingDb = competingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var competingTransaction = await competingDb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, timeout.Token);
            var competingBatch = await competingDb.StudentImportBatches.AsNoTracking()
                .SingleAsync((GymFlow.Domain.Entities.StudentImportBatch batch) => batch.Id == batchId, timeout.Token);
            Assert.Equal(0, competingBatch.State);
            var competingCandidate = await competingDb.Students
                .SingleAsync(student => student.Id == candidate.Id, timeout.Token);
            competingCandidate.Name = "Concurrent candidate after snapshot";
            try
            {
                await competingDb.SaveChangesAsync(timeout.Token);
                await competingTransaction.CommitAsync(timeout.Token);
                competingEditCommitted = true;
            }
            catch (Exception exception) when (ContainsPostgresSerializationFailure(exception))
            {
                competingEditSerializationConflict = true;
            }
        };

        var confirmationBody = new
        {
            newStudentsActive = true,
            decisions = new[]
            {
                new { rowNumber = 2, action = "ImportAsNew" },
                new { rowNumber = 3, action = "Import" }
            }
        };
        using var confirmation = await client.PostAsJsonAsync(
            $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
        var confirmationObservation = await ReadStudentImportResponseOnceAsync(confirmation, timeout.Token);
        var diagnosticFailures = new List<string>();
        var editRaceCategory = ClassifyStudentImportConflict(confirmationObservation.Message).Category;
        if (!interleavingReached) diagnosticFailures.Add("edit_interleaving_not_reached");
        if (competingEditCommitted == competingEditSerializationConflict)
            diagnosticFailures.Add("competing_transaction_outcome_not_exclusive");
        if (confirmationObservation.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.Conflict and
            not HttpStatusCode.ServiceUnavailable)
            diagnosticFailures.Add($"edit_race_http_{(int)confirmationObservation.StatusCode}");
        if (confirmationObservation.StatusCode == HttpStatusCode.Conflict && editRaceCategory is not
            ("review_snapshot_changed" or "batch_processing_or_expired" or "ef_optimistic_concurrency" or
             "postgres_serialization_40001"))
            diagnosticFailures.Add($"edit_race_conflict_category:{editRaceCategory}");
        if (confirmationObservation.StatusCode == HttpStatusCode.ServiceUnavailable &&
            editRaceCategory != "postgres_deadlock_40P01")
            diagnosticFailures.Add($"edit_race_unavailable_category:{editRaceCategory}");
        if (competingEditCommitted && confirmationObservation.StatusCode is not HttpStatusCode.Conflict and
            not HttpStatusCode.ServiceUnavailable)
            diagnosticFailures.Add("committed_concurrent_edit_was_not_rejected");

        if (confirmationObservation.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable)
        {
            var classification = ClassifyStudentImportConflict(confirmationObservation.Message);
            _testOutput.WriteLine($"EDIT_RACE_HTTP={(int)confirmationObservation.StatusCode}; CATEGORY={classification.Category}; MESSAGE={classification.SafeMessage}");
        }

        var firstAttemptImportCount = -1;
        var firstAttemptBatchState = -1;
        var firstAttemptPayloadPresent = false;
        var firstAttemptPreviewPresent = false;
        var candidateNameAfterRace = string.Empty;
        factory.WithDb(db =>
        {
            firstAttemptImportCount = db.Students.Count(student => student.GymId == factory.EnabledGym &&
                (student.Name == "Concurrent import synthetic" || student.Name == "Tenant-isolated import synthetic"));
            var savedBatch = db.StudentImportBatches.Single(batch => batch.Id == batchId);
            firstAttemptBatchState = savedBatch.State;
            firstAttemptPayloadPresent = savedBatch.PayloadJson is not null;
            firstAttemptPreviewPresent = savedBatch.PreviewJson is not null;
            candidateNameAfterRace = db.Students.Single(student => student.Id == candidate.Id).Name;
        });
        if ((confirmationObservation.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable) &&
            (firstAttemptImportCount != 0 || firstAttemptBatchState != 0 ||
             !firstAttemptPayloadPresent || !firstAttemptPreviewPresent))
            diagnosticFailures.Add("edit_concurrency_failure_was_not_atomic");
        if (confirmationObservation.StatusCode == HttpStatusCode.OK &&
            (firstAttemptImportCount != 2 || firstAttemptBatchState != 2))
            diagnosticFailures.Add("edit_confirmation_final_state_invalid");
        if ((candidateNameAfterRace == "Concurrent candidate after snapshot") != competingEditCommitted)
            diagnosticFailures.Add("concurrent_edit_state_did_not_match_transaction_outcome");

        var firstBatchConfirmationSucceeded = confirmationObservation.StatusCode == HttpStatusCode.OK;
        if (IsRetryableStudentImportConcurrencyFailure(confirmationObservation.StatusCode, editRaceCategory))
        {
            using var refreshed = await client.PostAsJsonAsync(
                "/api/students/imports/preview", new { rows = reviewRows }, timeout.Token);
            var refreshedObservation = await ReadStudentImportResponseOnceAsync(refreshed, timeout.Token);
            if (refreshedObservation.StatusCode != HttpStatusCode.OK || refreshedObservation.BatchId != batchId)
                diagnosticFailures.Add("fresh_preview_retry_failed");
            using var retry = await client.PostAsJsonAsync(
                $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
            var retryObservation = await ReadStudentImportResponseOnceAsync(retry, timeout.Token);
            firstBatchConfirmationSucceeded = retryObservation.StatusCode == HttpStatusCode.OK;
            if (!firstBatchConfirmationSucceeded)
                diagnosticFailures.Add($"fresh_preview_confirmation_http_{(int)retryObservation.StatusCode}");
        }

        var firstBatchReplaySucceeded = false;
        if (firstBatchConfirmationSucceeded)
        {
            using var replay = await client.PostAsJsonAsync(
                $"/api/students/imports/{batchId}/confirm", confirmationBody, timeout.Token);
            var replayObservation = await ReadStudentImportResponseOnceAsync(replay, timeout.Token);
            firstBatchReplaySucceeded = replayObservation.StatusCode == HttpStatusCode.OK &&
                replayObservation.AlreadyCompleted == true;
            if (!firstBatchReplaySucceeded) diagnosticFailures.Add("first_batch_replay_not_idempotent");
        }

        var concurrentRows = new[]
        {
            new StudentImportInputRow(4, "Same batch concurrent import", null, null, null)
        };
        using var secondPreview = await client.PostAsJsonAsync(
            "/api/students/imports/preview", new { rows = concurrentRows }, timeout.Token);
        var secondPreviewObservation = await ReadStudentImportResponseOnceAsync(secondPreview, timeout.Token);
        Guid? concurrentBatchId = secondPreviewObservation.BatchId;
        if (secondPreviewObservation.StatusCode != HttpStatusCode.OK || !concurrentBatchId.HasValue)
            diagnosticFailures.Add("same_batch_preview_failed");
        var concurrentBody = new
        {
            newStudentsActive = true,
            decisions = new[] { new { rowNumber = 4, action = "Import" } }
        };
        var concurrentObservations = new List<StudentImportHttpObservation>();
        if (concurrentBatchId.HasValue)
        {
            var responses = await Task.WhenAll(
                client.PostAsJsonAsync($"/api/students/imports/{concurrentBatchId.Value}/confirm", concurrentBody, timeout.Token),
                client.PostAsJsonAsync($"/api/students/imports/{concurrentBatchId.Value}/confirm", concurrentBody, timeout.Token));
            foreach (var response in responses)
                concurrentObservations.Add(await ReadStudentImportResponseOnceAsync(response, timeout.Token));
        }
        if (concurrentObservations.Count != 2 || concurrentObservations.Any(observation =>
                observation.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.Conflict and
                    not HttpStatusCode.ServiceUnavailable))
            diagnosticFailures.Add("same_batch_confirmation_http_status_invalid");
        if (!concurrentObservations.Any(observation =>
                observation.StatusCode == HttpStatusCode.OK && observation.AlreadyCompleted == false))
            diagnosticFailures.Add("same_batch_confirmation_no_fresh_success");
        foreach (var observation in concurrentObservations.Where(item =>
                     item.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable))
        {
            var classification = ClassifyStudentImportConflict(observation.Message);
            _testOutput.WriteLine($"SAME_BATCH_CONFIRM_HTTP={(int)observation.StatusCode}; CATEGORY={classification.Category}; MESSAGE={classification.SafeMessage}");
            if (observation.StatusCode == HttpStatusCode.Conflict && classification.Category is not
                ("postgres_serialization_40001" or "ef_optimistic_concurrency" or "batch_processing_or_expired"))
                diagnosticFailures.Add($"same_batch_confirm_conflict_category:{classification.Category}");
            if (observation.StatusCode == HttpStatusCode.ServiceUnavailable &&
                classification.Category != "postgres_deadlock_40P01")
                diagnosticFailures.Add($"same_batch_confirm_unavailable_category:{classification.Category}");
        }

        var firstBatchFinalImportCount = -1;
        var firstBatchFinalState = -1;
        var foreignStudentCount = -1;
        var foreignCandidateUnchanged = false;
        var concurrentBatchFinalImportCount = -1;
        var concurrentBatchFinalState = -1;
        factory.WithDb(db =>
        {
            firstBatchFinalImportCount = db.Students.Count(student => student.GymId == factory.EnabledGym &&
                (student.Name == "Concurrent import synthetic" || student.Name == "Tenant-isolated import synthetic"));
            firstBatchFinalState = db.StudentImportBatches.Single(batch => batch.Id == batchId).State;
            foreignStudentCount = db.Students.Count(student => student.GymId == factory.OtherEnabledGym);
            foreignCandidateUnchanged = db.Students.Any(student => student.Id == foreignCandidate.Id &&
                student.GymId == factory.OtherEnabledGym && student.Name == foreignCandidate.Name &&
                student.Email == foreignCandidate.Email);
            if (concurrentBatchId.HasValue)
            {
                concurrentBatchFinalImportCount = db.Students.Count(student => student.GymId == factory.EnabledGym &&
                    student.Name == "Same batch concurrent import");
                concurrentBatchFinalState = db.StudentImportBatches.Single(batch => batch.Id == concurrentBatchId.Value).State;
            }
        });
        if (firstBatchFinalImportCount != 2 || firstBatchFinalState != 2)
            diagnosticFailures.Add("first_batch_final_import_or_state_invalid");
        if (!firstBatchConfirmationSucceeded || !firstBatchReplaySucceeded)
            diagnosticFailures.Add("first_batch_confirmation_or_idempotency_failed");
        if (foreignStudentCount != 1 || !foreignCandidateUnchanged)
            diagnosticFailures.Add("gym_id_isolation_failed");
        if (concurrentBatchFinalImportCount != 1 || concurrentBatchFinalState != 2)
            diagnosticFailures.Add("same_batch_import_count_or_state_invalid");

        var concurrentBatchReplaySucceeded = false;
        if (concurrentBatchId.HasValue && concurrentBatchFinalState == 2)
        {
            using var replay = await client.PostAsJsonAsync(
                $"/api/students/imports/{concurrentBatchId.Value}/confirm", concurrentBody, timeout.Token);
            var replayObservation = await ReadStudentImportResponseOnceAsync(replay, timeout.Token);
            concurrentBatchReplaySucceeded = replayObservation.StatusCode == HttpStatusCode.OK &&
                replayObservation.AlreadyCompleted == true;
            if (!concurrentBatchReplaySucceeded) diagnosticFailures.Add("same_batch_replay_not_idempotent");
        }
        if (!concurrentBatchReplaySucceeded) diagnosticFailures.Add("same_batch_idempotency_not_confirmed");
        foreach (var diagnostic in factory.CapturedStudentImportDatabaseDiagnostics)
        {
            _testOutput.WriteLine($"SAFE_DATABASE_DIAGNOSTIC={diagnostic}");
            if (diagnostic.Contains("Category=postgres_serialization_40001", StringComparison.Ordinal) &&
                (!diagnostic.Contains("SqlState=40001", StringComparison.Ordinal) ||
                 !diagnostic.Contains("PostgresException", StringComparison.Ordinal)))
                diagnosticFailures.Add("serialization_diagnostic_missing_state_or_exception_type");
            if (diagnostic.Contains("Category=postgres_deadlock_40P01", StringComparison.Ordinal) &&
                (!diagnostic.Contains("SqlState=40P01", StringComparison.Ordinal) ||
                 !diagnostic.Contains("PostgresException", StringComparison.Ordinal)))
                diagnosticFailures.Add("deadlock_diagnostic_missing_state_or_exception_type");
        }
        if (diagnosticFailures.Count != 0)
        {
            _testOutput.WriteLine($"INTEGRITY_EVIDENCE=firstBatchCount:{firstBatchFinalImportCount}; firstBatchState:{firstBatchFinalState}; " +
                $"concurrentBatchCount:{concurrentBatchFinalImportCount}; concurrentBatchState:{concurrentBatchFinalState}; " +
                $"foreignGymCount:{foreignStudentCount}; foreignCandidateUnchanged:{foreignCandidateUnchanged}; " +
                $"firstAttemptCount:{firstAttemptImportCount}; firstAttemptState:{firstAttemptBatchState}; " +
                $"editConflictCategory:{(confirmationObservation.StatusCode == HttpStatusCode.Conflict ? ClassifyStudentImportConflict(confirmationObservation.Message).Category : "none")}");
            Assert.Fail("PostgreSQL import concurrency diagnostics: " + string.Join("; ", diagnosticFailures));
        }
    }

    private sealed record StudentImportHttpObservation(
        HttpStatusCode StatusCode,
        string? Message,
        Guid? BatchId,
        bool? AlreadyCompleted,
        IReadOnlyList<int>? CandidateCounts);

    private static async Task<StudentImportHttpObservation> ReadStudentImportResponseOnceAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using (response)
        {
            var statusCode = response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            string? message = root.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String ? messageElement.GetString() : null;
            Guid? batchId = root.TryGetProperty("batchId", out var batchElement) &&
                batchElement.TryGetGuid(out var parsedBatchId) ? parsedBatchId : null;
            bool? alreadyCompleted = root.TryGetProperty("alreadyCompleted", out var completedElement) &&
                completedElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? completedElement.GetBoolean() : null;
            IReadOnlyList<int>? candidateCounts = null;
            if (root.TryGetProperty("rows", out var rowsElement) && rowsElement.ValueKind == JsonValueKind.Array)
            {
                candidateCounts = rowsElement.EnumerateArray()
                    .Select(row => row.TryGetProperty("candidates", out var candidates) &&
                        candidates.ValueKind == JsonValueKind.Array ? candidates.GetArrayLength() : -1)
                    .ToArray();
            }
            return new(statusCode, message, batchId, alreadyCompleted, candidateCounts);
        }
    }

    private static (string Category, string SafeMessage) ClassifyStudentImportConflict(string? message)
    {
        var category = message switch
        {
            "Os dados exibidos dos possíveis duplicados mudaram. Gere uma nova prévia antes de continuar."
                => "review_snapshot_changed",
            "Esta prévia expirou ou já está em processamento. Gere uma nova prévia."
                => "batch_processing_or_expired",
            "A prévia não está mais disponível. Selecione o arquivo novamente."
                => "preview_unavailable",
            "A importação foi alterada por outra operação. Gere uma nova prévia."
                => "ef_optimistic_concurrency",
            "A importação concorreu com outra operação. Gere uma nova prévia."
                => "postgres_serialization_40001",
            "O banco está processando outra operação. Tente novamente em instantes."
                => "postgres_deadlock_40P01",
            "O serviço de dados está temporariamente indisponível. Tente novamente."
                => "postgres_connection_failure",
            "O banco está temporariamente indisponível. Tente novamente."
                => "postgres_transient_failure",
            null => "invalid_operation_empty_message",
            _ => "invalid_operation_unknown_message"
        };
        if (category is "review_snapshot_changed" or "batch_processing_or_expired" or "preview_unavailable" or
            "ef_optimistic_concurrency" or "postgres_serialization_40001")
            return (category, message!);

        var safe = message ?? string.Empty;
        safe = Regex.Replace(safe, @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[email]");
        safe = Regex.Replace(safe, @"(?i)(password|senha|passphrase|token|authorization)\s*[:=]\s*[^;\s,]+", "$1=[redacted]");
        safe = Regex.Replace(safe, @"\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b", "[id]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        safe = Regex.Replace(safe, @"'(?:[^']|'')*'", "'[value]'", RegexOptions.CultureInvariant);
        safe = Regex.Replace(safe, @"\b[A-Za-z0-9_-]{24,}\b", "[opaque]", RegexOptions.CultureInvariant);
        safe = safe.Replace("Concurrent candidate before", "[synthetic candidate]", StringComparison.Ordinal)
            .Replace("Concurrent candidate after snapshot", "[synthetic candidate]", StringComparison.Ordinal)
            .Replace("Concurrent import synthetic", "[synthetic import]", StringComparison.Ordinal)
            .Replace("Tenant-isolated import synthetic", "[synthetic import]", StringComparison.Ordinal)
            .Replace("Same batch concurrent import", "[synthetic import]", StringComparison.Ordinal);
        safe = new string(safe.Where(character => !char.IsControl(character) || character is '\r' or '\n')
            .Take(240).ToArray());
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message ?? string.Empty)));
        return (category, $"{safe}; chars={message?.Length ?? 0}; sha256={digest}");
    }

    private static bool ContainsPostgresSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException && postgresException.SqlState == "40001")
                return true;
        }
        return false;
    }

    private static bool IsRetryableStudentImportConcurrencyFailure(HttpStatusCode statusCode, string category) =>
        statusCode == HttpStatusCode.Conflict && category is
            ("review_snapshot_changed" or "ef_optimistic_concurrency" or "postgres_serialization_40001") ||
        statusCode == HttpStatusCode.ServiceUnavailable && category == "postgres_deadlock_40P01";

    private static void AssertActiveDisposablePostgresHarness()
    {
        var identity = DisposablePostgresCluster.CurrentOwnedIdentityForTests();
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var dataDirectory = Path.GetFullPath(identity.DataDirectory);
        Assert.True(Directory.Exists(dataDirectory));
        Assert.StartsWith(tempRoot, dataDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.False(DisposablePostgresCluster.IsReservedPort(identity.Port));
        Assert.False(DisposablePostgresCluster.IsProtectedDataDirectory(dataDirectory));
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_IntegratedImportedStudentFinancialTrainingAndAttendanceLifecycle(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres)
        {
            EnabledTimeZone = "America/Sao_Paulo"
        };
        var adminA = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var adminB = factory.AddUser(factory.DisabledGym, UserRole.Admin);
        var studentB = factory.AddUser(factory.DisabledGym);
        var emailA = $"cycle-{Guid.NewGuid():N}@example.invalid";
        var duplicateEmail = $"existing-{Guid.NewGuid():N}@example.invalid";
        var gymTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, gymTimeZone));
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Aluno pré-existente",
            Email = duplicateEmail, IsActive = true, AppAccessEnabled = false
        }));

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(adminA));
        var rows = new List<StudentImportInputRow>
        {
            new(2, "Aluno sintético do ciclo", emailA, "5511999990001", null),
            new(3, "Aluno sintético sem e-mail", null, null, null),
            new(4, "Duplicidade revisada", duplicateEmail, null, null)
        };
        var previewResponse = await adminClient.PostAsJsonAsync("/api/students/imports/preview", new { rows });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var batchId = (await previewResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("batchId").GetGuid();
        var confirmationBody = new
        {
            newStudentsActive = true,
            decisions = new[]
            {
                new { rowNumber = 2, action = "Import" },
                new { rowNumber = 3, action = "Import" },
                new { rowNumber = 4, action = "Ignore" }
            }
        };
        var importResponse = await adminClient.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", confirmationBody);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var importedResult = await importResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, importedResult.GetProperty("results").GetArrayLength());
        Assert.Equal("Ignored", importedResult.GetProperty("results")[2].GetProperty("status").GetString());
        Assert.Equal(2, importedResult.GetProperty("results").EnumerateArray()
            .Count(result => result.GetProperty("status").GetString() == "Imported"));
        var importRetry = await adminClient.PostAsJsonAsync($"/api/students/imports/{batchId}/confirm", confirmationBody);
        Assert.Equal(HttpStatusCode.OK, importRetry.StatusCode);
        Assert.True((await importRetry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alreadyCompleted").GetBoolean());

        Guid studentAId = default;
        Guid noEmailStudentId = default;
        Guid studentBId = default;
        factory.WithDb(db =>
        {
            studentAId = db.Students.Single(student => student.Email == emailA).Id;
            noEmailStudentId = db.Students.Single(student => student.Name == "Aluno sintético sem e-mail").Id;
            studentBId = db.Students.Single(student => student.UserId == studentB.Id).Id;
            foreach (var student in db.Students.Where(student => student.Id == studentAId || student.Id == noEmailStudentId))
            {
                Assert.Equal(factory.EnabledGym, student.GymId);
                Assert.Null(student.UserId);
                Assert.False(student.AppAccessEnabled);
            }
            Assert.Null(db.Students.Single(student => student.Id == noEmailStudentId).Email);
        });

        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(adminB));
        var featuresB = await adminClient.GetFromJsonAsync<JsonElement>("/api/gym-features");
        Assert.False(featuresB.GetProperty("erpEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await adminClient.PostAsJsonAsync("/api/students/imports/preview", new { rows })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await adminClient.PostAsJsonAsync("/api/plans", new { name = "Plano bloqueado", price = 100m,
                durationMonths = 1, billingCycle = PlanBillingCycle.Monthly })).StatusCode);

        // ERP-disabled tenant B retains the traditional student and training flows.
        var exerciseBName = $"Exercício tradicional {Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Created,
            (await adminClient.PostAsJsonAsync("/api/exercises", new { name = exerciseBName, muscleGroup = "Legs" })).StatusCode);
        var exercisesB = await adminClient.GetFromJsonAsync<JsonElement>("/api/exercises");
        var exerciseBId = exercisesB.GetProperty("items").EnumerateArray()
            .Single(exercise => exercise.GetProperty("name").GetString() == exerciseBName)
            .GetProperty("id").GetGuid();
        var daysB = G6TrainingDays(exerciseBId);
        var templateBResponse = await adminClient.PostAsJsonAsync("/api/workout-templates", new { name = "Treino tradicional B", days = daysB });
        Assert.Equal(HttpStatusCode.Created, templateBResponse.StatusCode);
        var templateBId = (await templateBResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var workoutBResponse = await adminClient.PostAsJsonAsync("/api/workouts/from-template", new
            { studentId = studentBId, templateId = templateBId, name = "Treino B", days = daysB });
        Assert.Equal(HttpStatusCode.Created, workoutBResponse.StatusCode);
        var workoutBId = (await workoutBResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var studentBClient = factory.CreateClient();
        var loginB = await studentBClient.PostAsJsonAsync("/api/auth/login", new { email = studentB.Email, password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, loginB.StatusCode); // ERP-disabled tenant does not require an enrollment.
        var tokenB = (await loginB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        studentBClient.DefaultRequestHeaders.Authorization = new("Bearer", tokenB);
        Assert.Equal(workoutBId,
            (await studentBClient.GetFromJsonAsync<JsonElement>("/api/workouts/me/current")).GetProperty("id").GetGuid());

        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(adminA));
        var planResponse = await adminClient.PostAsJsonAsync("/api/plans", new
        {
            name = "Plano sintético do ciclo", price = 100m, durationMonths = 1,
            billingCycle = PlanBillingCycle.Monthly
        });
        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);
        var planId = (await adminClient.GetFromJsonAsync<JsonElement>("/api/plans"))
            .EnumerateArray().Single().GetProperty("id").GetGuid();

        // Cross-tenant students and plans remain unusable even with otherwise valid payloads.
        var foreignPlanId = Guid.NewGuid();
        factory.WithDb(db => db.Plans.Add(new Plan
        {
            Id = foreignPlanId, GymId = factory.DisabledGym, Name = "Plano sintético B",
            Price = 100m, DurationMonths = 1, BillingCycle = PlanBillingCycle.Monthly, IsActive = true
        }));
        var paymentBody = new { paidAmount = 100m, discountAmount = 0m, paymentMethod = PaymentMethod.Pix, paidAt = DateTimeOffset.UtcNow };
        foreach (var (foreignStudentId, currentPlanId) in new[] { (studentBId, planId), (studentAId, foreignPlanId) })
        {
            var rejected = await adminClient.PostAsJsonAsync("/api/enrollments", new
            {
                studentId = foreignStudentId, planId = currentPlanId,
                startDate = today.AddYears(2), payment = paymentBody
            });
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        }

        var enrollmentResponse = await adminClient.PostAsJsonAsync("/api/enrollments", new
        {
            studentId = studentAId, planId, startDate = today,
            payment = paymentBody
        });
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        var enrollmentId = (await enrollmentResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var initialChargeId = (await adminClient.GetFromJsonAsync<JsonElement>("/api/charges"))
            .EnumerateArray().Single().GetProperty("id").GetGuid();

        // App access is granted explicitly by Admin and the single-use invite cannot be replayed.
        var appAccessResponse = await adminClient.PostAsJsonAsync($"/api/students/{studentAId}/app-access", new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, appAccessResponse.StatusCode);
        var inviteToken = (await appAccessResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(inviteToken));
        using var publicClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await publicClient.PostAsJsonAsync("/api/students/app-activate/validate", new { token = inviteToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await publicClient.PostAsJsonAsync("/api/students/app-activate", new { token = inviteToken, password = TestApi.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await publicClient.PostAsJsonAsync("/api/students/app-activate/validate", new { token = inviteToken })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await publicClient.PostAsJsonAsync("/api/students/app-activate", new { token = inviteToken, password = TestApi.Password })).StatusCode);

        var exerciseAName = $"Exercício do ciclo {Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Created,
            (await adminClient.PostAsJsonAsync("/api/exercises", new { name = exerciseAName, muscleGroup = "Legs" })).StatusCode);
        var exercisesA = await adminClient.GetFromJsonAsync<JsonElement>("/api/exercises");
        var exerciseAId = exercisesA.GetProperty("items").EnumerateArray()
            .Single(exercise => exercise.GetProperty("name").GetString() == exerciseAName)
            .GetProperty("id").GetGuid();
        var daysA = G6TrainingDays(exerciseAId);
        var templateAResponse = await adminClient.PostAsJsonAsync("/api/workout-templates", new { name = "Treino do ciclo", days = daysA });
        Assert.Equal(HttpStatusCode.Created, templateAResponse.StatusCode);
        var templateAId = (await templateAResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var workoutResponse = await adminClient.PostAsJsonAsync("/api/workouts/from-template", new
            { studentId = studentAId, templateId = templateAId, name = "Treino do aluno importado", days = daysA });
        Assert.Equal(HttpStatusCode.Created, workoutResponse.StatusCode);
        var workoutId = (await workoutResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var studentClient = factory.CreateClient();
        var studentLogin = await studentClient.PostAsJsonAsync("/api/auth/login", new { email = emailA, password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, studentLogin.StatusCode);
        var studentToken = (await studentLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        studentClient.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        Assert.Equal(workoutId,
            (await studentClient.GetFromJsonAsync<JsonElement>("/api/workouts/me/current")).GetProperty("id").GetGuid());

        var requestId = Guid.NewGuid();
        var checkInBody = new { studentId = studentAId, requestId };
        var checkInFirst = await adminClient.PostAsJsonAsync("/api/check-ins/manual", checkInBody);
        var checkInRetry = await adminClient.PostAsJsonAsync("/api/check-ins/manual", checkInBody);
        Assert.Equal(HttpStatusCode.OK, checkInFirst.StatusCode);
        Assert.Equal(HttpStatusCode.OK, checkInRetry.StatusCode);
        Assert.Equal((await checkInFirst.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await checkInRetry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        // Move the synthetic contract to yesterday: the existing app login policy denies access until renewal is paid.
        factory.WithDb(db => db.Enrollments.Single(enrollment => enrollment.Id == enrollmentId).EndDate = today.AddDays(-1));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = emailA, password = TestApi.Password })).StatusCode);
        var renewalResponse = await adminClient.PostAsJsonAsync($"/api/enrollments/{enrollmentId}/renew", new { planId });
        Assert.Equal(HttpStatusCode.Created, renewalResponse.StatusCode);
        var renewedEnrollmentId = (await renewalResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var renewedChargeId = (await adminClient.GetFromJsonAsync<JsonElement>("/api/charges"))
            .EnumerateArray().Single(charge => charge.GetProperty("enrollmentId").GetGuid() == renewedEnrollmentId)
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await adminClient.PatchAsJsonAsync($"/api/charges/{renewedChargeId}/confirm-payment", paymentBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await adminClient.PatchAsJsonAsync($"/api/charges/{renewedChargeId}/confirm-payment", paymentBody)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = emailA, password = TestApi.Password })).StatusCode);

        factory.WithDb(db =>
        {
            var cycleCharges = db.Charges
                .Where(charge => charge.Id == initialChargeId || charge.Id == renewedChargeId)
                .ToList();
            foreach (var charge in cycleCharges)
            {
                Assert.Equal(ChargeStatus.Paid, charge.Status);
                Assert.Equal(100m, charge.Amount);
                Assert.Equal(100m, charge.PaidAmount);
                Assert.Equal(0m, charge.DiscountAmount);
                Assert.Equal(PaymentMethod.Pix, charge.PaymentMethod);
                Assert.Equal(studentAId, db.Enrollments.Single(enrollment => enrollment.Id == charge.EnrollmentId).StudentId);
            }
            var expiredEnrollment = db.Enrollments.Single(enrollment => enrollment.Id == enrollmentId);
            Assert.Equal(EnrollmentStatus.Active, expiredEnrollment.Status);
            Assert.True(expiredEnrollment.EndDate < today);
            Assert.Equal(EnrollmentStatus.Active, db.Enrollments.Single(enrollment => enrollment.Id == renewedEnrollmentId).Status);
            Assert.Equal(2, db.FinancialAuditLogs.Count(log => log.GymId == factory.EnabledGym));
            Assert.All(db.FinancialAuditLogs.Where(log => log.GymId == factory.EnabledGym), log =>
            {
                Assert.Equal(adminA.Id, log.ActorUserId);
                Assert.Equal(FinancialAuditAction.PaymentConfirmed, log.Action);
            });
            Assert.Single(db.CheckIns.Where(checkIn => checkIn.GymId == factory.EnabledGym && checkIn.RequestId == requestId));
            Assert.Empty(db.PhysicalAccessEvents.Where(accessEvent => accessEvent.GymId == factory.EnabledGym));
            Assert.Equal(factory.DisabledGym, db.Students.Single(student => student.Id == studentBId).GymId);
            Assert.Equal(workoutBId, db.Workouts.Single(workout => workout.StudentId == studentBId).Id);
            Assert.Equal(factory.EnabledGym, db.Workouts.Single(workout => workout.Id == workoutId).GymId);
            Assert.Equal(3, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym && student.Email == duplicateEmail));
            Assert.Empty(db.Enrollments.Where(enrollment => enrollment.StudentId == studentBId));
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
