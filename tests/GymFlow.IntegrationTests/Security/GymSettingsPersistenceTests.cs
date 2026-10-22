using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.DTOs.GymSettings;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public Task GymSettings_PersistAuditAndRespectTimezoneAndTenant() => CheckGymSettingsPersistence(false);
    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_GymSettings_PersistAuditAndRespectTimezoneAndTenant(bool postgres) => CheckGymSettingsPersistence(postgres);

    private static async Task CheckGymSettingsPersistence(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        using var originalScope = factory.Services.CreateScope();
        var originalZones = originalScope.ServiceProvider.GetRequiredService<IGymTimeZoneProvider>();
        Assert.Equal(TimeSpan.Zero, originalZones.GetTimeZone(factory.LegacyGym).BaseUtcOffset);
        var student = factory.AddUser(factory.LegacyGym);
        var enrollmentId = factory.AddEnrollment(student, factory.LegacyGym, EnrollmentStatus.Active, -1, 20);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var create = await client.PutAsJsonAsync("/api/gym-settings", new UpdateGymSettingsRequest { AdministrativeName = "  Academia sintética  ", TimeZoneId = "UTC" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var first = (await create.Content.ReadFromJsonAsync<GymSettingsResponse>())!;
        Assert.Equal("Academia sintética", first.AdministrativeName);
        Assert.Equal(1, first.Version);
        Assert.Equal("persisted", first.TimeZoneSource);
        DateOnly endDate = default;
        factory.WithDb(db => endDate = db.Enrollments.Single(e => e.Id == enrollmentId).EndDate);
        var request = new UpdateGymSettingsRequest { AdministrativeName = "Academia atualizada", TimeZoneId = "America/Sao_Paulo", Version = first.Version };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/gym-settings", request)).StatusCode);
        request.ConfirmTimeZoneChange = true;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/gym-settings", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/gym-settings", request)).StatusCode);
        var read = (await client.GetFromJsonAsync<GymSettingsResponse>($"/api/gym-settings?gymId={factory.EnabledGym}"))!;
        Assert.Equal("America/Sao_Paulo", read.TimeZoneId);
        Assert.Equal(2, read.Version);
        using (var scope = factory.Services.CreateScope())
        {
            var zones = scope.ServiceProvider.GetRequiredService<IGymTimeZoneProvider>();
            var boundary = new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);
            Assert.Equal(new DateOnly(2026, 9, 30), DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(boundary, zones.GetTimeZone(factory.LegacyGym))));
            Assert.Equal(new DateOnly(2026, 10, 1), DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(boundary, zones.GetTimeZone(factory.EnabledGym))));
            Assert.NotSame(originalZones, zones);
            Assert.Same(zones.GetTimeZone(factory.LegacyGym), zones.GetTimeZone(factory.LegacyGym));
        }
        Assert.Equal(TimeSpan.Zero, originalZones.GetTimeZone(factory.LegacyGym).BaseUtcOffset); // Existing operation snapshot.
        factory.WithDb(db =>
        {
            Assert.Single(db.GymSettings);
            Assert.Equal(endDate, db.Enrollments.Single(e => e.Id == enrollmentId).EndDate);
            Assert.True(db.Users.Single(u => u.Id == student.Id).IsActive);
            var logs = db.GymSettingsAuditLogs.OrderBy(a => a.OccurredAt).ToList();
            Assert.Equal(2, logs.Count);
            Assert.All(logs, a => { Assert.Equal(factory.LegacyGym, a.GymId); Assert.Equal(admin.Id, a.ActorUserId); if (postgres) Assert.Equal(DateTimeKind.Utc, a.OccurredAt.Kind); });
            Assert.Equal("legacy", JsonDocument.Parse(logs[0].PreviousValues).RootElement.GetProperty("TimeZoneSource").GetString());
            Assert.Equal("UTC", JsonDocument.Parse(logs[1].PreviousValues).RootElement.GetProperty("TimeZoneId").GetString());
            Assert.Equal("America/Sao_Paulo", JsonDocument.Parse(logs[1].NewValues).RootElement.GetProperty("TimeZoneId").GetString());
            Assert.Empty(db.FinancialAuditLogs);
        });
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Null(configuration[$"GymFeatures:{factory.LegacyGym:D}:ErpEnabled"]);
        Assert.Equal("UTC", configuration[$"GymTimeZones:{factory.LegacyGym:D}"]);
        // No settings record is created by JWT or login; a legacy student needs no enrollment.
        var legacyUser = factory.AddUser(factory.DisabledGym);
        configuration[$"GymTimeZones:{factory.DisabledGym:D}"] = null;
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(legacyUser));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/gym-settings")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { legacyUser.Email, Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var unconfiguredUser = factory.AddUser(Guid.NewGuid()); // No ERP flag, settings row or legacy timezone.
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(unconfiguredUser));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/gym-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { unconfiguredUser.Email, Password = TestApi.Password })).StatusCode);
        factory.WithDb(db => Assert.Single(db.GymSettings));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GymSettings_ConcurrentSaveReturns409WithOneWinningAudit(bool existing) => CheckGymSettingsConcurrency(false, existing);
    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PostgreSql_GymSettings_ConcurrentSaveReturns409WithOneWinningAudit(bool existing) => CheckGymSettingsConcurrency(true, existing);

    private static async Task CheckGymSettingsConcurrency(bool postgres, bool existing)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        if (existing) Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/gym-settings", new UpdateGymSettingsRequest { AdministrativeName = "Initial", TimeZoneId = "UTC" })).StatusCode);
        factory.BeforeNextAsyncSave = async requestContext =>
        {
            using var scope = factory.Services.CreateScope();
            Assert.NotSame(requestContext, scope.ServiceProvider.GetRequiredService<AppDbContext>());
            await scope.ServiceProvider.GetRequiredService<GymSettingsService>().UpdateAsync(factory.LegacyGym, admin.Id,
                new UpdateGymSettingsRequest { AdministrativeName = "Winner", TimeZoneId = "UTC", Version = existing ? 1 : null });
        };
        var result = await client.PutAsJsonAsync("/api/gym-settings", new UpdateGymSettingsRequest { AdministrativeName = "Loser", TimeZoneId = "UTC", Version = existing ? 1 : null });
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.DoesNotContain("GymSettings", await result.Content.ReadAsStringAsync());
        factory.WithDb(db =>
        {
            Assert.Equal("Winner", db.GymSettings.Single().AdministrativeName);
            Assert.Equal(existing ? 2 : 1, db.GymSettings.Single().Version);
            Assert.Equal(existing ? 2 : 1, db.GymSettingsAuditLogs.Count());
            Assert.DoesNotContain(db.GymSettingsAuditLogs, a => a.NewValues.Contains("Loser"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GymSettings_AuditFailureRollsBackSettings(bool existing) => CheckGymSettingsAuditRollback(false, existing);
    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PostgreSql_GymSettings_AuditFailureRollsBackSettings(bool existing) => CheckGymSettingsAuditRollback(true, existing);

    private static async Task CheckGymSettingsAuditRollback(bool postgres, bool existing)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        if (existing)
        {
            using var initialScope = factory.Services.CreateScope();
            await initialScope.ServiceProvider.GetRequiredService<GymSettingsService>().UpdateAsync(factory.LegacyGym, admin.Id, new UpdateGymSettingsRequest { AdministrativeName = "Initial", TimeZoneId = "UTC" });
        }
        using (var scope = factory.Services.CreateScope())
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<GymSettingsService>().UpdateAsync(factory.LegacyGym, Guid.NewGuid(),
                new UpdateGymSettingsRequest { AdministrativeName = "Must rollback", TimeZoneId = "UTC", Version = existing ? 1 : null }));
        factory.WithDb(db =>
        {
            Assert.Equal(existing ? 1 : 0, db.GymSettings.Count());
            Assert.Equal(existing ? 1 : 0, db.GymSettingsAuditLogs.Count());
            if (existing) { Assert.Equal("Initial", db.GymSettings.Single().AdministrativeName); Assert.Equal(1, db.GymSettings.Single().Version); }
        });
    }

    [Theory]
    [InlineData("", "UTC")]
    [InlineData("   ", "UTC")]
    [InlineData("Valid", "E. South America Standard Time")]
    [InlineData("Valid", "invalid/zone")]
    public async Task GymSettings_RejectsInvalidFields(string name, string zone)
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/gym-settings", new UpdateGymSettingsRequest { AdministrativeName = name, TimeZoneId = zone, ConfirmTimeZoneChange = true })).StatusCode);
        factory.WithDb(db => { Assert.Empty(db.GymSettings); Assert.Empty(db.GymSettingsAuditLogs); });
    }

    [Theory]
    [InlineData("gymId")]
    [InlineData("erpEnabled")]
    [InlineData("secretHash")]
    [InlineData("actorUserId")]
    public async Task GymSettings_RejectsForbiddenWriteProperties(string property)
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var body = new Dictionary<string, object?> { ["administrativeName"] = "Synthetic", ["timeZoneId"] = "UTC", [property] = factory.EnabledGym.ToString() };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/gym-settings", body)).StatusCode);
        factory.WithDb(db => { Assert.Empty(db.GymSettings); Assert.Empty(db.GymSettingsAuditLogs); });
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_GymSettings_MigrationIsAdditive(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var userId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var legacyName = "Synthetic legacy user";
        var legacyEmail = $"{userId:N}@example.invalid";
        var passwordHash = "synthetic-hash";
        var planName = "Synthetic legacy plan";
        const decimal price = 100m;
        const int durationMonths = 1;
        var billingCycle = (int)PlanBillingCycle.Monthly;
        var role = (int)UserRole.Student;
        var enrollmentStatus = (int)EnrollmentStatus.Active;
        var start = DateOnly.FromDateTime(createdAt).AddDays(-1);
        var originalEnd = start.AddDays(21);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261011034816_AddCheckIns");

            // This is a historical schema from before SeparateStudentIdentity. Seed its
            // real legacy shape directly; the current EF model includes columns that did
            // not exist at this migration and cannot safely be used to write into it.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id", "GymId", "Name", "Email", "PasswordHash", "Role", "IsActive", "CreatedAt")
                VALUES ({userId}, {factory.LegacyGym}, {legacyName}, {legacyEmail}, {passwordHash}, {role}, {true}, {createdAt});
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Students" ("Id", "UserId", "CreatedAt")
                VALUES ({studentId}, {userId}, {createdAt});
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Plans" ("Id", "GymId", "Name", "Price", "DurationMonths", "BillingCycle", "IsActive", "CreatedAt")
                VALUES ({planId}, {factory.LegacyGym}, {planName}, {price}, {durationMonths}, {billingCycle}, {true}, {createdAt});
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Enrollments" ("Id", "StudentId", "PlanId", "StartDate", "EndDate", "Status", "PlanName", "PlanPrice", "PlanDurationMonths", "PlanBillingCycle", "CreatedAt")
                VALUES ({enrollmentId}, {studentId}, {planId}, {start}, {originalEnd}, {enrollmentStatus}, {planName}, {price}, {durationMonths}, {billingCycle}, {createdAt});
                """);
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261012203545_AddGymSettingsAndAudit", await db.Database.GetAppliedMigrationsAsync());
        }
        factory.WithDb(db =>
        {
            Assert.True(db.Users.Single(u => u.Id == userId).IsActive);
            Assert.Equal(originalEnd, db.Enrollments.Single(e => e.Id == enrollmentId).EndDate);
            Assert.Empty(db.GymSettings);
            Assert.Empty(db.GymSettingsAuditLogs);
        });
    }
}
