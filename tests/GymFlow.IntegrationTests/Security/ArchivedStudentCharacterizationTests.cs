using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    // Regressions for the archival inconsistencies characterized in #121, stage 2B.
    [Theory]
    [InlineData("enabled", false, 0)]
    [InlineData("disabled", false, 0)]
    [InlineData("missing", false, 0)]
    [InlineData("enabled", true, 0)]
    [InlineData("enabled", false, 10)]
    public Task ArchivedPayment_RequiresErpAndPreservesInactivityAndArchive(string mode, bool manual, int startOffset) =>
        CheckArchivedPayment(false, mode, manual, startOffset);

    [PostgresTheory]
    [InlineData("enabled", false, 0)]
    [InlineData("disabled", false, 0)]
    [InlineData("missing", false, 0)]
    [InlineData("enabled", true, 0)]
    [InlineData("enabled", false, 10)]
    public Task PostgreSql_ArchivedPayment(string mode, bool manual, int startOffset) =>
        CheckArchivedPayment(true, mode, manual, startOffset);

    private static async Task CheckArchivedPayment(bool postgres, string mode, bool manual, int startOffset)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var gym = mode == "enabled" ? factory.EnabledGym : mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        var user = factory.AddUser(gym, active: false);
        var enrollmentId = factory.AddEnrollment(user, gym, EnrollmentStatus.PendingPayment, startOffset, 40);
        var chargeId = Guid.NewGuid();
        var archivedAt = AtPostgresPrecision(DateTime.UtcNow.AddDays(-1));
        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100);
        var reason = manual ? StudentInactivationReason.Manual : StudentInactivationReason.NoValidEnrollment;
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            student.ArchivedAt = archivedAt;
            student.InactivationReason = reason;
            student.InactivatedAt = archivedAt.AddDays(-60);
            student.NoValidEnrollmentSince = since;
            db.Charges.Add(new Charge
            {
                Id = chargeId, EnrollmentId = enrollmentId, Amount = 100,
                Status = ChargeStatus.Pending, DueDate = since
            });
        });

        var before = G6FinancialState(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var response = await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", new
        {
            PaidAmount = 100, DiscountAmount = 0, PaymentMethod = PaymentMethod.Pix, PaidAt = DateTimeOffset.UtcNow
        });
        var erpEnabled = mode == "enabled";
        Assert.Equal(erpEnabled ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.UserId == user.Id);
            Assert.Equal(archivedAt, student.ArchivedAt);
            Assert.False(student.User.IsActive);
            Assert.Equal(reason, student.InactivationReason);
            Assert.Equal(since, student.NoValidEnrollmentSince);
            Assert.Equal(archivedAt.AddDays(-60), student.InactivatedAt);
            Assert.Equal(erpEnabled ? EnrollmentStatus.Active : EnrollmentStatus.PendingPayment, db.Enrollments.Single().Status);
            var charge = db.Charges.Single();
            if (!erpEnabled)
            {
                Assert.Equal(ChargeStatus.Pending, charge.Status);
                Assert.Null(charge.PaidAmount);
                Assert.Null(charge.PaidAt);
                Assert.Null(charge.PaymentMethod);
                Assert.Empty(db.FinancialAuditLogs);
                return;
            }
            Assert.Equal(ChargeStatus.Paid, charge.Status);
            Assert.Equal(100m, charge.PaidAmount);
            var audit = Assert.Single(db.FinancialAuditLogs);
            Assert.Equal(FinancialAuditAction.PaymentConfirmed, audit.Action);
            Assert.Equal(admin.Id, audit.ActorUserId);
            Assert.Equal(gym, audit.GymId);
            Assert.Equal(chargeId, audit.EntityId);
        });
        if (!erpEnabled) Assert.Equal(before, G6FinancialState(factory));
        await AssertArchivedRegressionAppAccessAsync(factory, user, false);
    }

    [Theory]
    [InlineData("enabled", true, true)]
    [InlineData("enabled", true, false)]
    [InlineData("disabled", true, false)]
    [InlineData("missing", true, false)]
    [InlineData("enabled", false, true)]
    [InlineData("missing", false, false)]
    public Task ArchivedIdentity_IsIgnoredByLifecycleButDeniedByLoginAndExistingJwt(string mode, bool active, bool validEnrollment) =>
        CheckArchivedIdentity(false, mode, active, validEnrollment);

    [PostgresTheory]
    [InlineData("enabled", true, true)]
    [InlineData("enabled", true, false)]
    [InlineData("disabled", true, false)]
    [InlineData("missing", true, false)]
    [InlineData("enabled", false, true)]
    [InlineData("missing", false, false)]
    public Task PostgreSql_ArchivedIdentity(string mode, bool active, bool validEnrollment) =>
        CheckArchivedIdentity(true, mode, active, validEnrollment);

    private static async Task CheckArchivedIdentity(bool postgres, string mode, bool active, bool validEnrollment)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var gym = mode == "enabled" ? factory.EnabledGym : mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var user = factory.AddUser(gym, active: active);
        if (validEnrollment) factory.AddEnrollment(user, gym, EnrollmentStatus.Active, -1, 30);
        var tokenBeforeArchive = factory.Token(user);
        await AssertArchivedRegressionAppAccessAsync(factory, user, active && (validEnrollment || mode != "enabled"), tokenBeforeArchive);
        var archivedAt = AtPostgresPrecision(DateTime.UtcNow.AddDays(-1));
        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            student.ArchivedAt = archivedAt;
            student.NoValidEnrollmentSince = since;
        });

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<StudentLifecycleService>().ProcessAsync(gym);
        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.UserId == user.Id);
            Assert.Equal(archivedAt, student.ArchivedAt);
            Assert.Equal(active, student.User.IsActive);
            Assert.Equal(since, student.NoValidEnrollmentSince);
            Assert.Equal(active ? (StudentInactivationReason?)null : StudentInactivationReason.Manual, student.InactivationReason);
        });
        await AssertArchivedRegressionAppAccessAsync(factory, user, false, tokenBeforeArchive);
    }

    [Theory]
    [InlineData("create", false, 0)]
    [InlineData("create", false, 10)]
    [InlineData("create", true, 0)]
    [InlineData("create", true, 10)]
    [InlineData("payment", false, 0)]
    public Task FinancialOperation_ConcurrentLifecycleArchiveReturns409AndRollsBackAllFinancialWrites(string operation, bool active, int startOffset) =>
        CheckConcurrentArchive(false, operation, active, startOffset);

    [PostgresTheory]
    [InlineData("create", false, 0)]
    [InlineData("create", false, 10)]
    [InlineData("create", true, 0)]
    [InlineData("create", true, 10)]
    [InlineData("payment", false, 0)]
    public Task PostgreSql_FinancialOperation_ConcurrentArchive(string operation, bool active, int startOffset) =>
        CheckConcurrentArchive(true, operation, active, startOffset);

    private static async Task CheckConcurrentArchive(bool postgres, string operation, bool active, int startOffset)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var gym = factory.EnabledGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        var user = factory.AddUser(gym, active: active);
        var planId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();
        var studentId = Guid.Empty;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            studentId = student.Id;
            student.InactivationReason = active ? null : StudentInactivationReason.NoValidEnrollment;
            student.NoValidEnrollmentSince = today.AddDays(-100);
            db.Plans.Add(new Plan
            {
                Id = planId, GymId = gym, Name = "Synthetic archive race plan",
                Price = 100, DurationMonths = 1, IsActive = true
            });
            if (operation == "payment")
                db.Enrollments.Add(new Enrollment
                {
                    Id = Guid.NewGuid(), StudentId = studentId, PlanId = planId,
                    PlanName = "Synthetic archive race plan", PlanPrice = 100, PlanDurationMonths = 1,
                    Status = EnrollmentStatus.PendingPayment, StartDate = today, EndDate = today.AddMonths(1),
                    Charge = new Charge { Id = chargeId, Amount = 100, Status = ChargeStatus.Pending, DueDate = today }
                });
        });
        DateTime? concurrentArchive = null;
        bool? concurrentUserActive = null;
        StudentInactivationReason? concurrentInactivationReason = null;
        var archiveUsedIndependentContext = false;
        Func<AppDbContext, Task> archive = async requestContext =>
        {
            // EnrollmentService already read a non-archived student.
            // The real lifecycle now archives it in an independent context before financial writes.
            using var scope = factory.Services.CreateScope();
            var competing = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            archiveUsedIndependentContext = !ReferenceEquals(requestContext, competing);
            await scope.ServiceProvider.GetRequiredService<StudentLifecycleService>().ProcessAsync(gym);
            var archived = await competing.Students.Include(s => s.User).SingleAsync(s => s.Id == studentId);
            concurrentArchive = postgres && archived.ArchivedAt is { } date ? AtPostgresPrecision(date) : archived.ArchivedAt;
            concurrentUserActive = archived.User?.IsActive;
            concurrentInactivationReason = archived.InactivationReason;
        };
        if (operation == "create") factory.BeforeNextTransaction = archive;
        else factory.BeforeNextAsyncSave = archive;

        var financialSaveObserved = false;
        var financialSaveHadTransaction = false;
        var financialSaveEnrollmentCount = 0;
        var financialSaveChargeCount = 0;
        var financialSaveAuditCount = 0;
        if (operation == "create") factory.AfterNextAsyncSave = db =>
        {
            // The first async save belongs to the competing lifecycle. Observe the
            // following financial save before the student's concurrency check fails.
            factory.AfterNextAsyncSave = financialDb =>
            {
                financialSaveHadTransaction = financialDb.Database.CurrentTransaction is not null;
                financialSaveEnrollmentCount = financialDb.Enrollments.Count();
                financialSaveChargeCount = financialDb.Charges.Count();
                financialSaveAuditCount = financialDb.FinancialAuditLogs.Count();
                financialSaveObserved = true;
            };
        };

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var payment = new { PaidAmount = 100, PaymentMethod = PaymentMethod.Pix, PaidAt = DateTimeOffset.UtcNow };
        var response = operation == "create"
            ? await client.PostAsJsonAsync("/api/enrollments", new
            {
                StudentId = studentId, PlanId = planId, StartDate = today.AddDays(startOffset), Payment = payment
            })
            : await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", payment);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict,
            $"Operação financeira concorrente retornou {response.StatusCode}. Response: {responseBody}{Environment.NewLine}Server exceptions:{Environment.NewLine}{factory.CapturedExceptions}");
        Assert.True(archiveUsedIndependentContext);
        Assert.NotNull(concurrentArchive);
        Assert.Equal(active, concurrentUserActive);
        Assert.Equal(StudentInactivationReason.NoValidEnrollment, concurrentInactivationReason);
        if (operation == "create")
        {
            Assert.True(financialSaveObserved);
            Assert.True(financialSaveHadTransaction);
            Assert.Equal(1, financialSaveEnrollmentCount);
            Assert.Equal(1, financialSaveChargeCount);
            Assert.Equal(1, financialSaveAuditCount);
        }
        var payload = JsonDocument.Parse(responseBody).RootElement;
        Assert.Equal("Os dados foram alterados por outra operação. Atualize os dados antes de tentar novamente.", payload.GetProperty("message").GetString());
        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.Id == studentId);
            Assert.Equal(concurrentArchive, student.ArchivedAt);
            Assert.Equal(active, student.User.IsActive);
            Assert.Equal(StudentInactivationReason.NoValidEnrollment, student.InactivationReason);
            Assert.Equal(today.AddDays(-100), student.NoValidEnrollmentSince);
            if (operation == "create")
            {
                Assert.Empty(db.Enrollments);
                Assert.Empty(db.Charges);
            }
            else
            {
                Assert.Equal(EnrollmentStatus.PendingPayment, Assert.Single(db.Enrollments).Status);
                var charge = Assert.Single(db.Charges);
                Assert.Equal(ChargeStatus.Pending, charge.Status);
                Assert.Null(charge.PaidAmount);
                Assert.Null(charge.PaidAt);
            }
            Assert.Empty(db.FinancialAuditLogs);
        });
        await AssertArchivedRegressionAppAccessAsync(factory, user, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Lifecycle_StaleArchiveDoesNotUndoCommittedEnrollment(bool active) => CheckStaleArchive(false, active);

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PostgreSql_Lifecycle_StaleArchive(bool active) => CheckStaleArchive(true, active);

    private static async Task CheckStaleArchive(bool postgres, bool active)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var gym = factory.EnabledGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        var user = factory.AddUser(gym, active: active);
        var planId = Guid.NewGuid();
        var studentId = Guid.Empty;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            studentId = student.Id;
            student.InactivationReason = active ? null : StudentInactivationReason.NoValidEnrollment;
            student.NoValidEnrollmentSince = today.AddDays(-100);
            db.Plans.Add(new Plan { Id = planId, GymId = gym, Name = "Synthetic winning enrollment", Price = 100, DurationMonths = 1, IsActive = true });
        });
        var enrollmentCommitted = false;
        factory.BeforeNextAsyncSave = async lifecycleContext =>
        {
            using var scope = factory.Services.CreateScope();
            Assert.NotSame(lifecycleContext, scope.ServiceProvider.GetRequiredService<AppDbContext>());
            var enrollment = await scope.ServiceProvider.GetRequiredService<EnrollmentService>().CreateAsync(
                gym, admin.Id, new GymFlow.Application.DTOs.Enrollments.CreateEnrollmentRequest
                {
                    StudentId = studentId, PlanId = planId, StartDate = today,
                    Payment = new GymFlow.Application.DTOs.Charges.PaymentDetailsRequest
                    { PaidAmount = 100, PaymentMethod = PaymentMethod.Pix, PaidAt = DateTimeOffset.UtcNow }
                });
            Assert.NotNull(enrollment);
            enrollmentCommitted = true;
        };
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<StudentLifecycleService>().ProcessAsync(gym);
        Assert.True(enrollmentCommitted);
        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.Id == studentId);
            Assert.Null(student.ArchivedAt);
            Assert.Equal(active, student.User!.IsActive);
            Assert.Null(student.InactivationReason);
            Assert.Null(student.NoValidEnrollmentSince);
            Assert.Single(db.Enrollments);
            Assert.Equal(ChargeStatus.Paid, Assert.Single(db.Charges).Status);
            Assert.Single(db.FinancialAuditLogs);
        });
        await AssertArchivedRegressionAppAccessAsync(factory, user, active);
    }

    // PostgreSQL timestamps persist microseconds; synthetic expectations use that precision.
    private static DateTime AtPostgresPrecision(DateTime value) => new(value.Ticks - value.Ticks % 10, value.Kind);

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ArchiveConcurrencyMappingUsesMigratedTimestampColumns(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: true);
        var user = factory.AddUser(factory.EnabledGym);
        var timestamp = AtPostgresPrecision(DateTime.UtcNow);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            student.ArchivedAt = timestamp;
            student.UpdatedAt = timestamp;
        });
        factory.WithDb(db =>
        {
            Assert.NotEmpty(db.Database.GetAppliedMigrations());
            Assert.Empty(db.Database.GetPendingMigrations());
            var student = db.Students.Single(s => s.UserId == user.Id);
            Assert.Equal(timestamp, student.ArchivedAt);
            Assert.Equal(timestamp, student.UpdatedAt);
            foreach (var name in new[] { nameof(Student.ArchivedAt), nameof(Student.UpdatedAt) })
                Assert.True(db.Model.FindEntityType(typeof(Student))!.FindProperty(name)!.IsConcurrencyToken);
            db.Database.OpenConnection();
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Students' AND column_name IN ('ArchivedAt', 'UpdatedAt') ORDER BY column_name";
            using var reader = command.ExecuteReader();
            foreach (var name in new[] { nameof(Student.ArchivedAt), nameof(Student.UpdatedAt) })
            {
                Assert.True(reader.Read());
                Assert.Equal(name, reader.GetString(0));
                Assert.Equal("timestamp with time zone", reader.GetString(1));
            }
            Assert.False(reader.Read());
        });
    }

    private static async Task AssertArchivedRegressionAppAccessAsync(TestApi factory, User user, bool allowed, string? existingToken = null)
    {
        using var client = factory.CreateClient();
        var expected = allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
        var login = await client.PostAsJsonAsync("/api/auth/login", new { user.Email, Password = TestApi.Password });
        Assert.Equal(expected, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", existingToken ?? factory.Token(user));
        foreach (var endpoint in new[] { "/api/auth/me", "/api/students/me" })
            Assert.Equal(expected, (await client.GetAsync(endpoint)).StatusCode);
        if (allowed)
        {
            var payload = await login.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new("Bearer", payload.GetProperty("token").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/students/me")).StatusCode);
        }
    }
}
