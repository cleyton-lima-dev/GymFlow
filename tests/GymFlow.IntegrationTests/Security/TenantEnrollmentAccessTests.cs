using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Theory]
    [InlineData("profile")]
    [InlineData("status")]
    [InlineData("create")]
    [InlineData("reactivate")]
    [InlineData("payment")]
    public Task ConcurrentAdministrativeChanges_Return409WithoutPartialWrites(string operation) =>
        CheckConcurrentHttpOperation(operation, false);

    [PostgresTheory]
    [InlineData("profile")]
    [InlineData("status")]
    [InlineData("create")]
    [InlineData("reactivate")]
    [InlineData("payment")]
    public Task PostgreSql_ConcurrentAdministrativeChanges_Return409WithoutPartialWrites(string operation) =>
        CheckConcurrentHttpOperation(operation, true);

    private static async Task CheckConcurrentHttpOperation(string operation, bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var user = factory.AddUser(factory.EnabledGym, active: false);
        var planId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();
        Guid studentId = default;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            studentId = student.Id;
            student.InactivationReason = StudentInactivationReason.NoValidEnrollment;
            student.NoValidEnrollmentSince = today.AddDays(-40);
            if (operation == "reactivate") student.ArchivedAt = DateTime.UtcNow.AddDays(-1);
            var plan = new Plan { Id = planId, GymId = factory.EnabledGym, Name = "Synthetic payment plan", Price = 100, DurationMonths = 1, IsActive = true };
            db.Plans.Add(plan);
            if (operation == "payment")
                db.Enrollments.Add(new Enrollment
                {
                    Id = Guid.NewGuid(), StudentId = studentId, Plan = plan, PlanName = plan.Name,
                    PlanPrice = 100, PlanDurationMonths = 1, Status = EnrollmentStatus.PendingPayment,
                    StartDate = today, EndDate = today.AddMonths(1),
                    Charge = new Charge { Id = chargeId, Amount = 100, Status = ChargeStatus.Pending, DueDate = today }
                });
        });
        var financialSaveObserved = false;
        Func<AppDbContext, Task> block = async requestContext =>
        {
            using var adminScope = factory.Services.CreateScope();
            var competing = adminScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotSame(requestContext, competing);
            if (operation == "reactivate")
            {
                var student = await competing.Students.Include(s => s.User).SingleAsync(s => s.Id == studentId);
                student.InactivationReason = StudentInactivationReason.Manual;
                student.InactivatedAt = DateTime.UtcNow;
                student.User.IsActive = false;
                await competing.SaveChangesAsync();
            }
            else
                Assert.True(await adminScope.ServiceProvider.GetRequiredService<StudentService>()
                    .UpdateStatusAsync(studentId, factory.EnabledGym, false));

            if (operation == "create")
                factory.AfterNextAsyncSave = db =>
                {
                    Assert.NotNull(db.Database.CurrentTransaction);
                    Assert.Single(db.Enrollments.AsNoTracking());
                    Assert.Equal(ChargeStatus.Paid, db.Charges.AsNoTracking().Single().Status);
                    Assert.Single(db.FinancialAuditLogs.AsNoTracking());
                    financialSaveObserved = true;
                };
        };
        // Creation reads the student before starting its explicit transaction.
        // Block then, so the first financial save succeeds and the second save
        // detects the stale reason. Other operations use their save boundary.
        if (operation == "create") factory.BeforeNextTransaction = block;
        else factory.BeforeNextAsyncSave = block;

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var payment = new { PaidAmount = 100, DiscountAmount = 0, PaymentMethod = PaymentMethod.Pix, PaidAt = DateTimeOffset.UtcNow };
        var response = operation switch
        {
            "profile" => await client.PutAsJsonAsync($"/api/students/{studentId}", new { Name = "Must rollback", user.Email, Phone = "123" }),
            "status" => await client.PatchAsJsonAsync($"/api/students/{studentId}/status", new { IsActive = true }),
            "create" => await client.PostAsJsonAsync("/api/enrollments", new { StudentId = studentId, PlanId = planId, StartDate = today, Payment = payment }),
            "reactivate" => await client.PostAsJsonAsync($"/api/students/{studentId}/reactivate", new { PlanId = planId, StartDate = today }),
            _ => await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", payment)
        };
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Os dados foram alterados por outra operação. Atualize os dados antes de tentar novamente.", payload.GetProperty("message").GetString());
        Assert.Equal(2, payload.EnumerateObject().Count()); // Only generic message and traceId.
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("traceId").GetString()));
        if (operation == "create") Assert.True(financialSaveObserved);
        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.Id == studentId);
            Assert.False(student.User.IsActive);
            Assert.Equal(StudentInactivationReason.Manual, student.InactivationReason);
            Assert.Equal("Synthetic user", student.User.Name);
            Assert.Null(student.Phone);
            Assert.Equal(operation == "reactivate", student.ArchivedAt is not null);
            Assert.Empty(db.FinancialAuditLogs);
            if (operation == "payment")
            {
                Assert.Equal(EnrollmentStatus.PendingPayment, db.Enrollments.Single().Status);
                var charge = db.Charges.Single();
                Assert.Equal(ChargeStatus.Pending, charge.Status);
                Assert.Null(charge.PaidAt);
                Assert.Null(charge.PaidAmount);
            }
            else
            {
                Assert.Empty(db.Enrollments);
                Assert.Empty(db.Charges);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FinancialCreation_CommitsAllOrRollsBackUnexpectedFailure(bool fail) => CheckFinancialCreation(false, fail);

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PostgreSql_FinancialCreation_CommitsAllOrRollsBackUnexpectedFailure(bool fail) => CheckFinancialCreation(true, fail);

    private static async Task CheckFinancialCreation(bool postgres, bool fail)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var user = factory.AddUser(factory.EnabledGym, active: false);
        var planId = Guid.NewGuid();
        Guid studentId = default;
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            studentId = student.Id;
            student.InactivationReason = StudentInactivationReason.NoValidEnrollment;
            db.Plans.Add(new Plan { Id = planId, GymId = factory.EnabledGym, Name = "Synthetic plan", Price = 100, DurationMonths = 1, IsActive = true });
        });
        var firstSaveObserved = false;
        factory.AfterNextAsyncSave = db =>
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            Assert.Single(db.Enrollments.AsNoTracking());
            Assert.Single(db.Charges.AsNoTracking());
            Assert.Single(db.FinancialAuditLogs.AsNoTracking());
            firstSaveObserved = true;
            if (fail) factory.BeforeNextAsyncSave = _ => throw new InvalidOperationException("synthetic-sensitive-internal-detail");
        };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var response = await client.PostAsJsonAsync("/api/enrollments", new
        {
            StudentId = studentId, PlanId = planId, StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Payment = new { PaidAmount = 100, PaymentMethod = PaymentMethod.Pix, PaidAt = DateTimeOffset.UtcNow }
        });
        Assert.True(firstSaveObserved);
        Assert.Equal(fail ? HttpStatusCode.InternalServerError : HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("synthetic-sensitive-internal-detail", await response.Content.ReadAsStringAsync());
        factory.WithDb(db =>
        {
            Assert.Equal(fail ? 0 : 1, db.Enrollments.Count());
            Assert.Equal(fail ? 0 : 1, db.Charges.Count());
            Assert.Equal(fail ? 0 : 1, db.FinancialAuditLogs.Count());
            var student = db.Students.Include(s => s.User).Single(s => s.Id == studentId);
            Assert.Equal(!fail, student.User.IsActive);
            Assert.Equal(fail ? StudentInactivationReason.NoValidEnrollment : (StudentInactivationReason?)null, student.InactivationReason);
            if (!fail)
            {
                Assert.Equal(ChargeStatus.Paid, db.Charges.Single().Status);
                Assert.Equal(100, db.Charges.Single().PaidAmount);
                Assert.Equal(FinancialAuditAction.PaymentConfirmed, db.FinancialAuditLogs.Single().Action);
            }
        });
    }

    [Theory]
    [InlineData("enabled", false, 0)]
    [InlineData("enabled", false, 10)]
    [InlineData("enabled", true, 0)]
    [InlineData("enabled", true, 10)]
    [InlineData("disabled", false, 0)]
    [InlineData("disabled", false, 10)]
    [InlineData("missing", false, 0)]
    [InlineData("missing", false, 10)]
    [InlineData("disabled", true, 0)]
    [InlineData("disabled", true, 10)]
    [InlineData("missing", true, 0)]
    [InlineData("missing", true, 10)]
    public async Task Reactivation_UsesTenantPolicyAndPreservesManualBlock(string mode, bool manual, int start)
    {
        await using var factory = new TestApi();
        var gym = mode == "enabled" ? factory.EnabledGym : mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var user = factory.AddUser(gym, active: false);
        var planId = Guid.NewGuid();
        Guid studentId = default;
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            studentId = student.Id;
            student.ArchivedAt = DateTime.UtcNow.AddDays(-1);
            student.InactivationReason = manual ? StudentInactivationReason.Manual : StudentInactivationReason.NoValidEnrollment;
            student.NoValidEnrollmentSince = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100);
            db.Plans.Add(new Plan { Id = planId, GymId = gym, Name = "Synthetic plan", Price = 100, DurationMonths = 1, IsActive = true });
        });
        using (var scope = factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<EnrollmentService>().ReactivateArchivedStudentAsync(
                gym, studentId, new GymFlow.Application.DTOs.Students.ReactivateArchivedStudentRequest
                { PlanId = planId, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(start) });
            Assert.NotNull(result);
        }
        var allowed = !manual && (mode != "enabled" || start == 0);
        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.Id == studentId);
            Assert.Null(student.ArchivedAt);
            Assert.Equal(allowed, student.User.IsActive);
            Assert.Equal(manual ? StudentInactivationReason.Manual : allowed ? (StudentInactivationReason?)null : StudentInactivationReason.NoValidEnrollment,
                student.InactivationReason);
            if (allowed) Assert.Null(student.NoValidEnrollmentSince);
            var enrollment = db.Enrollments.Single(e => e.StudentId == studentId);
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
            Assert.Equal(planId, enrollment.PlanId);
            Assert.Equal(100, enrollment.PlanPrice);
            Assert.Empty(db.Charges);
        });
        using var client = factory.CreateClient();
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new { user.Email, Password = TestApi.Password })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("reactivate")]
    [InlineData("archive")]
    [InlineData("other-tenant")]
    public async Task Lifecycle_ConcurrentManualBlockIsPreservedWithIndependentContexts(string scenario)
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.EnabledGym, active: scenario == "archive");
        var other = factory.AddUser(factory.LegacyGym);
        var companion = factory.AddUser(factory.EnabledGym, active: false);
        Guid studentId = default;
        Guid otherId = default;
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.UserId == user.Id);
            studentId = student.Id;
            otherId = db.Students.Single(s => s.UserId == other.Id).Id;
            student.InactivationReason = scenario == "archive" ? null : StudentInactivationReason.NoValidEnrollment;
            student.NoValidEnrollmentSince = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100);
            db.Students.Single(s => s.UserId == companion.Id).InactivationReason = StudentInactivationReason.NoValidEnrollment;
        });
        factory.AddEnrollment(companion, factory.EnabledGym, EnrollmentStatus.Active, -1, 1);
        if (scenario != "archive")
            factory.AddEnrollment(user, factory.EnabledGym, EnrollmentStatus.Active, -1, 1);

        // Runs after the lifecycle read and before its SaveChanges transaction.
        // Admin uses a different scoped DbContext and the real status service.
        factory.BeforeNextAsyncSave = async lifecycleContext =>
        {
            using var adminScope = factory.Services.CreateScope();
            var adminContext = adminScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotSame(lifecycleContext, adminContext);
            Assert.True(await adminScope.ServiceProvider.GetRequiredService<StudentService>().UpdateStatusAsync(
                scenario == "other-tenant" ? otherId : studentId,
                scenario == "other-tenant" ? factory.LegacyGym : factory.EnabledGym, false));
        };
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<StudentLifecycleService>().ProcessAsync(factory.EnabledGym);

        factory.WithDb(db =>
        {
            var student = db.Students.Include(s => s.User).Single(s => s.Id == studentId);
            var otherStudent = db.Students.Include(s => s.User).Single(s => s.Id == otherId);
            Assert.Null(student.ArchivedAt); // A conflicting archive batch must roll back too.
            Assert.Equal(scenario == "other-tenant", student.User.IsActive);
            Assert.Equal(scenario == "other-tenant" ? (StudentInactivationReason?)null : StudentInactivationReason.Manual, student.InactivationReason);
            Assert.Equal(scenario != "other-tenant", otherStudent.User.IsActive);
            if (scenario == "other-tenant") Assert.Equal(StudentInactivationReason.Manual, otherStudent.InactivationReason);
            Assert.Equal(scenario == "other-tenant", db.Users.Single(u => u.Id == companion.Id).IsActive);
        });
        // A fresh pass must also respect the manual reason.
        if (scenario == "reactivate")
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<StudentLifecycleService>().ProcessAsync(factory.EnabledGym);
            factory.WithDb(db => Assert.False(db.Users.Single(u => u.Id == user.Id).IsActive));
        }
    }

    [Theory]
    [InlineData("enabled", false)]
    [InlineData("disabled", true)]
    [InlineData("missing", true)]
    public async Task LoginAndAuthenticatedEndpoints_UseOnlyTheirOwnTenantPolicy(string mode, bool allowed)
    {
        await using var factory = new TestApi();
        var gym = mode switch
        {
            "enabled" => factory.EnabledGym,
            "disabled" => factory.DisabledGym,
            _ => factory.LegacyGym
        };
        var user = factory.AddUser(gym);
        using var client = factory.CreateClient();

        // Client-provided flags and GymId must not override the persisted identity.
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user.Email, Password = TestApi.Password, GymId = factory.LegacyGym, ErpEnabled = false
        });
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, login.StatusCode);

        // A previously issued token must obey the same policy as a new login.
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        foreach (var endpoint in new[] { "/api/auth/me", "/api/students/me" })
        {
            var response = await client.GetAsync(endpoint);
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
            if (allowed)
            {
                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var idProperty = endpoint == "/api/auth/me" ? "userId" : "id";
                Assert.True(json.RootElement.TryGetProperty(idProperty, out _));
            }
        }
    }

    [Theory]
    [InlineData(EnrollmentStatus.Active, -1, 1, true)]
    [InlineData(EnrollmentStatus.Active, 0, 1, true)]
    [InlineData(EnrollmentStatus.Active, 1, 2, false)]
    [InlineData(EnrollmentStatus.Active, -1, 0, false)]
    [InlineData(EnrollmentStatus.PendingPayment, -1, 1, false)]
    [InlineData(EnrollmentStatus.Cancelled, -1, 1, false)]
    [InlineData(EnrollmentStatus.Expired, -1, 1, false)]
    public async Task EnrollmentStateAndLocalDates_AreEnforcedInLoginAndJwt(
        EnrollmentStatus status, int startOffset, int endOffset, bool allowed)
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.EnabledGym);
        factory.AddEnrollment(user, factory.EnabledGym, status, startOffset, endOffset);
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { user.Email, Password = TestApi.Password });
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task EnrollmentWithAnotherTenantsPlan_DoesNotGrantAccess()
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.EnabledGym);
        factory.AddEnrollment(user, factory.LegacyGym, EnrollmentStatus.Active, -1, 1);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { user.Email, Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherStudentsEnrollment_DoesNotGrantAccess(bool anotherTenant)
    {
        await using var factory = new TestApi();
        var withoutEnrollment = factory.AddUser(factory.EnabledGym);
        var enrolledGym = anotherTenant ? factory.LegacyGym : factory.EnabledGym;
        var enrolled = factory.AddUser(enrolledGym);
        factory.AddEnrollment(enrolled, enrolledGym, EnrollmentStatus.Active, -1, 1);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(withoutEnrollment));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(enrolled));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public void InvalidPolicy_PreventsApiStartup()
    {
        using var factory = new TestApi { EnabledFlag = "invalid" };
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task InvalidJwtSignature_IsRejectedEvenForLegacyTenant()
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.LegacyGym);
        var parts = factory.Token(user).Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ExistingJwt_IsRejectedAfterEnrollmentCancellation()
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.EnabledGym);
        var id = factory.AddEnrollment(user, factory.EnabledGym, EnrollmentStatus.Active, -1, 1);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        factory.WithDb(db => db.Enrollments.Single(e => e.Id == id).Status = EnrollmentStatus.Cancelled);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManuallyInactiveStudent_IsBlockedRegardlessOfErp(bool enabled)
    {
        await using var factory = new TestApi();
        var gym = enabled ? factory.EnabledGym : factory.LegacyGym;
        var user = factory.AddUser(gym, active: false);
        factory.AddEnrollment(user, gym, EnrollmentStatus.Active, -1, 1);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { user.Email, Password = TestApi.Password })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Admin, "/api/auth/admin")]
    [InlineData(UserRole.Professor, "/api/auth/me")]
    public async Task NonStudentUsers_DoNotRequireEnrollment(UserRole role, string endpoint)
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.EnabledGym, role);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { user.Email, Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", payload.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(endpoint)).StatusCode);
        if (role == UserRole.Professor)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/admin")).StatusCode);
    }

    [Fact]
    public async Task AccessAgent_StillUsesIdentityActivityAndCredentialVersion()
    {
        await using var factory = new TestApi();
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Synthetic agent",
            MachineName = "isolated-test", SecretHash = "unused", CredentialVersion = 1
        };
        factory.WithDb(db => db.AccessAgents.Add(agent));
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/access-agent/control-plane/heartbeat", new { pendingOfflineEvents = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/admin")).StatusCode);

        factory.WithDb(db => db.AccessAgents.Single(a => a.Id == agent.Id).CredentialVersion++);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/access-agent/control-plane/heartbeat", new { pendingOfflineEvents = 0 })).StatusCode);
    }

    [Fact]
    public async Task UserIdentityCannotBeMovedToAnotherTenantBySignedClaims()
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.EnabledGym);
        user.GymId = factory.LegacyGym; // Deliberately sign inconsistent claims with the test issuer.
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Lifecycle_ProcessesOnlyEnabledTenant()
    {
        await using var factory = new TestApi();
        var enabled = factory.AddUser(factory.EnabledGym);
        var legacy = factory.AddUser(factory.LegacyGym);
        var disabled = factory.AddUser(factory.DisabledGym);
        factory.WithDb(db =>
        {
            foreach (var student in db.Students)
                student.NoValidEnrollmentSince = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100);
        });
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<StudentLifecycleService>();
            await service.ProcessAsync(factory.LegacyGym);
            await service.ProcessAsync(factory.DisabledGym);
            await service.ProcessAsync(factory.EnabledGym);
        }
        factory.WithDb(db =>
        {
            Assert.False(db.Users.Single(u => u.Id == enabled.Id).IsActive);
            Assert.True(db.Users.Single(u => u.Id == legacy.Id).IsActive);
            Assert.True(db.Users.Single(u => u.Id == disabled.Id).IsActive);
            Assert.NotNull(db.Students.Single(s => s.UserId == enabled.Id).ArchivedAt);
            Assert.Null(db.Students.Single(s => s.UserId == legacy.Id).ArchivedAt);
            Assert.Null(db.Students.Single(s => s.UserId == disabled.Id).ArchivedAt);
        });
    }

    private sealed class TestApi(bool usePostgres = false) : WebApplicationFactory<Program>
    {
        public const string Password = "synthetic-password-123";
        public Guid EnabledGym { get; } = Guid.NewGuid();
        public Guid DisabledGym { get; } = Guid.NewGuid();
        public Guid LegacyGym { get; } = Guid.NewGuid();
        public string EnabledFlag { get; init; } = "true";
        public Func<AppDbContext, Task>? BeforeNextAsyncSave { get; set; }
        public Func<AppDbContext, Task>? BeforeNextTransaction { get; set; }
        public Action<AppDbContext>? AfterNextAsyncSave { get; set; }
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private string? _postgresAdmin;
        private string? _postgresDatabase;
        private bool _initialized;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Host settings reach the minimal entry point before its startup validation.
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["environment"] = "Testing",
                ["Jwt:Key"] = "synthetic-test-key-only-01234567890123456789",
                ["Jwt:Issuer"] = "isolated-tests",
                ["Jwt:Audience"] = "isolated-tests",
                ["Jwt:ExpirationMinutes"] = "30",
                ["ConnectionStrings:DefaultConnection"] = "Host=unused.invalid;Database=unused",
                ["StudentLifecycle:Enabled"] = "false",
                // Even a legacy global true must have no effect.
                ["EnrollmentAccess:EnforceStudentEnrollment"] = "true",
                [$"GymFeatures:{EnabledGym:D}:ErpEnabled"] = EnabledFlag,
                [$"GymFeatures:{DisabledGym:D}:ErpEnabled"] = "false",
                [$"GymTimeZones:{DisabledGym:D}"] = "UTC",
                [$"GymTimeZones:{LegacyGym:D}"] = "UTC",
                [$"GymTimeZones:{EnabledGym:D}"] = "UTC"
            }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                // Replace all provider configuration; never open the application connection.
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                if (usePostgres)
                {
                    var isolated = new NpgsqlConnectionStringBuilder(
                        Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES"));
                    if (isolated.Host != "127.0.0.1" || isolated.Port != 55439 ||
                        isolated.Database != "gymflow_codex_test_base" || isolated.Username != "gymflow_test")
                        throw new InvalidOperationException("Only the explicitly isolated local test cluster is allowed.");
                    isolated.Pooling = false;
                    _postgresAdmin = isolated.ConnectionString;
                    _postgresDatabase = $"gymflow_codex_test_{Guid.NewGuid():N}";
                    using var admin = new NpgsqlConnection(_postgresAdmin);
                    admin.Open();
                    using var create = new NpgsqlCommand($"CREATE DATABASE \"{_postgresDatabase}\"", admin);
                    create.ExecuteNonQuery();
                    isolated.Database = _postgresDatabase;
                    services.AddDbContext<AppDbContext>(options => options.UseNpgsql(isolated.ConnectionString)
                        .AddInterceptors(new BeforeSaveInterceptor(this), new BeforeTransactionInterceptor(this)));
                }
                else
                {
                    _connection.Open();
                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection)
                        .AddInterceptors(new BeforeSaveInterceptor(this), new BeforeTransactionInterceptor(this)));
                }
            });
        }

        public void WithDb(Action<AppDbContext> action)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!_initialized)
            {
                db.Database.EnsureCreated();
                _initialized = true;
            }
            action(db);
            db.SaveChanges();
        }

        public User AddUser(Guid gymId, UserRole role = UserRole.Student, bool active = true)
        {
            using var scope = Services.CreateScope();
            var user = new User
            {
                Id = Guid.NewGuid(), GymId = gymId, Role = role, IsActive = active,
                Name = "Synthetic user", Email = $"{Guid.NewGuid():N}@example.invalid",
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password)
            };
            WithDb(db =>
            {
                db.Users.Add(user);
                if (role == UserRole.Student)
                    db.Students.Add(new Student
                    {
                        Id = Guid.NewGuid(), UserId = user.Id,
                        InactivationReason = active ? null : StudentInactivationReason.Manual
                    });
            });
            return user;
        }

        public Guid AddEnrollment(User user, Guid planGym, EnrollmentStatus status, int start, int end)
        {
            var id = Guid.NewGuid();
            WithDb(db =>
            {
                var plan = new Plan
                {
                    Id = Guid.NewGuid(), GymId = planGym, Name = $"Plan-{id:N}", Price = 100,
                    DurationMonths = 1
                };
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                db.Enrollments.Add(new Enrollment
                {
                    Id = id, StudentId = db.Students.Single(s => s.UserId == user.Id).Id,
                    Plan = plan, PlanName = plan.Name, PlanPrice = plan.Price,
                    PlanDurationMonths = 1, Status = status,
                    StartDate = today.AddDays(start), EndDate = today.AddDays(end)
                });
            });
            return id;
        }

        public string Token(User user)
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
            if (_postgresDatabase is not null)
            {
                await using var admin = new NpgsqlConnection(_postgresAdmin);
                await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_postgresDatabase}\" WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }

        private sealed class BeforeSaveInterceptor(TestApi factory) : SaveChangesInterceptor
        {
            public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
                CancellationToken cancellationToken = default)
            {
                var callback = factory.AfterNextAsyncSave;
                factory.AfterNextAsyncSave = null;
                callback?.Invoke((AppDbContext)eventData.Context!);
                return ValueTask.FromResult(result);
            }

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                var callback = factory.BeforeNextAsyncSave;
                factory.BeforeNextAsyncSave = null;
                if (callback is not null) await callback((AppDbContext)eventData.Context!);
                return result;
            }
        }

        private sealed class BeforeTransactionInterceptor(TestApi factory) : DbTransactionInterceptor
        {
            public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
                DbConnection connection, TransactionStartingEventData eventData,
                InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
            {
                var callback = factory.BeforeNextTransaction;
                factory.BeforeNextTransaction = null;
                if (callback is not null) await callback((AppDbContext)eventData.Context!);
                return result;
            }
        }
    }

    public sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")))
                Skip = "Explicitly isolated local PostgreSQL test cluster is not configured.";
        }
    }
}
