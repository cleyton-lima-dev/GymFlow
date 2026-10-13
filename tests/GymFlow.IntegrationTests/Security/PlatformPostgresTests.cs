using System.Net;
using System.Net.Http.Json;
using GymFlow.Application.DTOs.Platform;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

public sealed class PlatformPostgresTests
{
    private static readonly ProvisionGymRequest Request = new("Academia sintética PG", "America/Sao_Paulo", "Admin sintético PG", "admin-pg@example.test", false);

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_FirstOperatorUniquenessGuardPreventsConcurrentBootstrap(bool postgres)
    {
        await using var app = new PlatformOnboardingTests.TestApi(postgres); app.Initialize(seedOperator: false);
        using var firstScope = app.Services.CreateScope(); using var secondScope = app.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IPlatformRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IPlatformRepository>();
        Assert.False(await first.HasOperatorAsync()); Assert.False(await second.HasOperatorAsync());
        PlatformOperator Actor(IServiceProvider services, string email) => new()
        {
            Id = PlatformOnboardingTests.TestApi.OperatorId, Name = "Operador sintético", Email = email,
            PasswordHash = services.GetRequiredService<IPasswordHasher>().Hash(PlatformOnboardingTests.TestApi.Password),
            ProtectedTotpSecret = services.GetRequiredService<IPlatformSecurity>().ProtectSecret(PlatformOnboardingTests.TestApi.Secret),
            CreatedAt = DateTime.UtcNow
        };
        async Task<bool> Save(IPlatformRepository repository, PlatformOperator actor)
        {
            try { await repository.SaveOperatorAsync(actor, create: true); return true; }
            catch (PlatformConflictException) { return false; }
        }
        var results = await Task.WhenAll(Save(first, Actor(firstScope.ServiceProvider, "first@example.test")),
            Save(second, Actor(secondScope.ServiceProvider, "second@example.test")));
        Assert.Single(results, success => success);
        using var verification = app.Services.CreateScope();
        Assert.Single(verification.ServiceProvider.GetRequiredService<AppDbContext>().PlatformOperators);
    }

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_MigrationAndConcurrentProvisionAreAtomicAndIdempotent(bool postgres)
    {
        await using var app = new PlatformOnboardingTests.TestApi(postgres); app.Initialize();
        var key = Guid.NewGuid().ToString(); SetBarrier(app);
        var results = await Task.WhenAll(app.Provision(Request, key), app.Provision(Request, key));
        Assert.Equal(results[0].GymId, results[1].GymId);
        Assert.Single(results, x => x.Token is not null); Assert.Single(results, x => x.Replayed);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains((await db.Database.GetAppliedMigrationsAsync()), x => x.EndsWith("AddPlatformOnboarding"));
        Assert.Single(db.Users); Assert.Single(db.GymSettings); Assert.Single(db.GymOnboardings);
        Assert.Single(db.AdminActivationInvitations); Assert.Single(db.GymOnboardingAuditLogs);
        Assert.False(scope.ServiceProvider.GetRequiredService<IGymErpPolicy>().IsEnabled(results[0].GymId));
    }

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentActivationReturns409AndCommitsExactlyOnce(bool postgres)
    {
        await using var app = new PlatformOnboardingTests.TestApi(postgres); app.Initialize();
        var invitation = await app.Provision(Request); SetBarrier(app);
        using var first = app.CreateClient(); using var second = app.CreateClient();
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invitation.Token!, PlatformOnboardingTests.TestApi.Password)),
            second.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invitation.Token!, "different-synthetic-password")));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await db.Users.SingleAsync()).IsActive);
        Assert.Equal(2, await db.GymOnboardingAuditLogs.CountAsync());
        Assert.Equal(2, (await db.GymOnboardings.SingleAsync()).Version);
        Assert.NotNull((await db.AdminActivationInvitations.SingleAsync()).ConsumedAt);
    }

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentStepsReturn409WithoutLosingAudit(bool postgres)
    {
        await using var app = new PlatformOnboardingTests.TestApi(postgres); app.Initialize();
        var invitation = await app.Provision(Request); SetBarrier(app);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await app.OperatorToken(client));
        var path = $"/api/platform/gym-onboardings/{invitation.GymId}/steps";
        var responses = await Task.WhenAll(client.PatchAsJsonAsync(path, new OnboardingStepRequest(1, "configuration", null)),
            client.PatchAsJsonAsync(path, new OnboardingStepRequest(1, "configuration", null)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.GymOnboardingAuditLogs.CountAsync());
    }

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_AuditDatabaseFailureRollsBackEveryInsertedRow(bool postgres)
    {
        await using var app = new PlatformOnboardingTests.TestApi(postgres); app.Initialize();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Fault injected inside PostgreSQL, after other INSERT statements can execute.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fail_synthetic_onboarding_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Synthetic audit failure' USING ERRCODE = '23514'; END; $$;
                CREATE TRIGGER fail_synthetic_audit BEFORE INSERT ON "GymOnboardingAuditLogs"
                FOR EACH ROW EXECUTE FUNCTION fail_synthetic_onboarding_audit();
                """);
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => app.Provision(Request));
        using var verification = app.Services.CreateScope(); var actual = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(actual.Users); Assert.Empty(actual.GymSettings); Assert.Empty(actual.GymOnboardings);
        Assert.Empty(actual.AdminActivationInvitations); Assert.Empty(actual.GymOnboardingAuditLogs);
        Assert.Single(actual.PlatformOperators);
    }

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_IndependentTenantsAndMfaReplayAreIsolated(bool postgres)
    {
        await using var app = new PlatformOnboardingTests.TestApi(postgres); app.Initialize();
        var first = await app.Provision(Request);
        var second = await app.Provision(Request with { AdministrativeName = "Outra academia sintética", AdminEmail = "other-pg@example.test" });
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(first.Token!, PlatformOnboardingTests.TestApi.Password))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Request.AdminEmail, password = PlatformOnboardingTests.TestApi.Password });
        var session = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());
        var settings = await client.GetStringAsync($"/api/gym-settings?gymId={second.GymId}");
        Assert.Contains(Request.AdministrativeName, System.Text.RegularExpressions.Regex.Unescape(settings));
        Assert.DoesNotContain("Outra academia", settings);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/platform/gym-onboardings/{second.GymId}")).StatusCode);
        using var other = app.CreateClient();
        var attempts = await Task.WhenAll(app.Login(other), app.Login(other));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Unauthorized);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.Users.SingleAsync(x => x.GymId == second.GymId)).IsActive);
        Assert.Equal(2, await db.GymSettings.CountAsync());
    }

    private static void SetBarrier(PlatformOnboardingTests.TestApi app)
    {
        var count = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.BeforeSave = async _ => {
            if (Interlocked.Increment(ref count) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(20));
        };
    }
}
