using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using GymFlow.Api.Platform;
using GymFlow.Application.DTOs.Platform;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
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

namespace GymFlow.IntegrationTests.Security;

public sealed class PlatformOnboardingTests
{
    private static readonly ProvisionGymRequest Request = new("Academia sintética", "America/Sao_Paulo", "Admin sintético", "admin77@example.test", false);

    [Theory]
    [InlineData(59, "94287082")]
    [InlineData(1111111109, "07081804")]
    [InlineData(1111111111, "14050471")]
    [InlineData(1234567890, "89005924")]
    [InlineData(2000000000, "69279037")]
    [InlineData(20000000000, "65353130")]
    public void Totp_MatchesRfc6238Vectors(long seconds, string expected) =>
        Assert.Equal(expected, PlatformTotp.Generate(TestApi.Secret, seconds / 30, 8));

    [Fact]
    public async Task Login_RequiresPasswordAndMfaRejectsReplayAndLocksRepeatedFailures()
    {
        await using var app = new TestApi(); using var client = app.CreateClient();
        app.Initialize();
        var invalid = await client.PostAsJsonAsync("/api/platform/auth/login", new PlatformLoginRequest(TestApi.Email, TestApi.Password, "invalid"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var good = await app.Login(client);
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Login(client)).StatusCode);
        app.Clock.Now = app.Clock.Now.AddSeconds(30);
        Assert.Equal(HttpStatusCode.OK, (await app.Login(client)).StatusCode);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/platform/auth/login", new PlatformLoginRequest(TestApi.Email, "wrong-password", "123456"))).StatusCode);
        app.Clock.Now = app.Clock.Now.AddSeconds(30);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Login(client)).StatusCode);
        app.Clock.Now = app.Clock.Now.AddMinutes(16);
        Assert.NotNull(await app.InScope(service => service.LoginAsync(new PlatformLoginRequest(TestApi.Email,
            TestApi.Password, PlatformTotp.Generate(TestApi.Secret, app.Clock.Now.ToUnixTimeSeconds() / 30)))));
    }

    [Fact]
    public async Task Jwt_SeparatesPlatformAcademyAndAccessAgentAndRevokesOperators()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
        var token = await app.OperatorToken(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
        foreach (var path in new[] { "/api/gym-settings", "/api/auth/me", "/api/access-agents", "/api/students" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        foreach (var role in Enum.GetValues<UserRole>())
        {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User { Id = Guid.NewGuid(), GymId = Guid.NewGuid(), Name = "Synthetic", Email = $"{Guid.NewGuid()}@example.test", Role = role, PasswordHash = "unused" };
            db.Users.Add(user); await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
        }
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var agent = new AccessAgent { Id = Guid.NewGuid(), GymId = Guid.NewGuid(), Name = "Synthetic", SecretHash = "unused" };
            db.AccessAgents.Add(agent); await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new("Bearer", scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
            var actor = await db.PlatformOperators.SingleAsync(); actor.IsActive = false; await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
    }

    [Fact]
    public void DisabledPlatform_DoesNotRequireMfaSecretsOrAffectLegacyStartup()
    {
        var services = new ServiceCollection();
        services.AddAvelriPlatform(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Platform:Enabled"] = "false", ["Platform:MfaEncryptionKey"] = "invalid-unused-value" }).Build());
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<PlatformAuthOptions>().Enabled);
    }

    [Theory]
    [InlineData("missing-mfa")]
    [InlineData("shared-jwt-key")]
    [InlineData("shared-issuer")]
    public void EnabledPlatform_RefusesUnsafeSecurityConfiguration(string scenario)
    {
        var settings = new Dictionary<string, string?> {
            ["Platform:Enabled"] = "true", ["Jwt:Key"] = "synthetic-academy-key-01234567890123456789",
            ["Jwt:Issuer"] = "academy", ["Jwt:Audience"] = "academy",
            ["Platform:Jwt:Key"] = "synthetic-platform-key-01234567890123456789",
            ["Platform:MfaEncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        };
        if (scenario == "missing-mfa") settings["Platform:MfaEncryptionKey"] = null;
        if (scenario == "shared-jwt-key") settings["Platform:Jwt:Key"] = settings["Jwt:Key"];
        if (scenario == "shared-issuer") settings["Platform:Jwt:Issuer"] = settings["Jwt:Issuer"];
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAvelriPlatform(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }

    [Fact]
    public async Task PlatformPolicy_RejectsMissingMfaAndReturns403ForWrongRoleEvenWithValidPlatformSignature()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        var options = app.Services.GetRequiredService<PlatformAuthOptions>();
        foreach (var scenario in new[] { (Mfa: "pwd", Role: "PlatformOperator", Status: HttpStatusCode.Unauthorized),
            (Mfa: "mfa", Role: "Admin", Status: HttpStatusCode.Forbidden) })
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(options.Issuer, options.Audience,
                [new("sub", TestApi.OperatorId.ToString()), new("identity_type", "platform_operator"),
                    new("amr", scenario.Mfa), new("role", scenario.Role), new("credential_version", "1")],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
            Assert.Equal(scenario.Status, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
            Assert.Equal(scenario.Status, (await Provision(client, Guid.NewGuid().ToString(), Request)).StatusCode);
        }
    }

    [Fact]
    public async Task Bootstrap_RefusesProductionBeforeAnyDatabaseAccess()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions
            { EnvironmentName = "Production", ContentRootPath = Path.GetTempPath() });
        await using var app = builder.Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => PlatformBootstrap.RunAsync(app));
    }

    [Fact]
    public async Task MfaSecret_IsEncryptedAndTamperingCannotAuthenticate()
    {
        await using var app = new TestApi(); app.Initialize();
        using var scope = app.Services.CreateScope();
        var security = scope.ServiceProvider.GetRequiredService<IPlatformSecurity>();
        var actor = (await scope.ServiceProvider.GetRequiredService<IPlatformRepository>().GetOperatorAsync(TestApi.OperatorId))!;
        Assert.NotEqual(TestApi.Secret, actor.ProtectedTotpSecret);
        Assert.DoesNotContain(TestApi.Secret, actor.ProtectedTotpSecret);
        var code = PlatformTotp.Generate(TestApi.Secret, app.Clock.Now.ToUnixTimeSeconds() / 30);
        Assert.NotNull(security.ValidateTotp(actor.ProtectedTotpSecret, code, app.Clock.Now, -1));
        var encrypted = Convert.FromBase64String(actor.ProtectedTotpSecret); encrypted[^1] ^= 1;
        Assert.Null(security.ValidateTotp(Convert.ToBase64String(encrypted), code, app.Clock.Now, -1));
    }

    [Fact]
    public async Task Provision_IsIdempotentAtomicAuditedAndNeverEnablesErp()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await app.OperatorToken(client));
        var key = Guid.NewGuid().ToString();
        var first = await Provision(client, key, Request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = (await first.Content.ReadFromJsonAsync<InvitationResult>())!;
        Assert.NotEqual(Guid.Empty, result.GymId); Assert.NotNull(result.Token);
        Assert.Equal(app.Clock.Now.UtcDateTime.AddHours(24), result.ExpiresAt);
        var replay = (await (await Provision(client, key, Request)).Content.ReadFromJsonAsync<InvitationResult>())!;
        Assert.True(replay.Replayed); Assert.Equal(result.GymId, replay.GymId); Assert.Null(replay.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await Provision(client, key, Request with { AdministrativeName = "Different" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Provision(client, Guid.NewGuid().ToString(), Request)).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(db.GymSettings); Assert.Single(db.GymOnboardings); Assert.Single(db.Users);
        var admin = await db.Users.SingleAsync(); Assert.False(admin.IsActive); Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(result.GymId, admin.GymId);
        Assert.Equal("America/Sao_Paulo", (await db.GymSettings.SingleAsync()).TimeZoneId);
        Assert.Single(db.GymOnboardingAuditLogs);
        Assert.DoesNotContain(result.Token!, (await db.GymOnboardingAuditLogs.SingleAsync()).Changes);
        Assert.NotEqual(result.Token, (await db.AdminActivationInvitations.SingleAsync()).TokenHash);
        Assert.False(scope.ServiceProvider.GetRequiredService<IGymErpPolicy>().IsEnabled(result.GymId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = Request.AdminEmail, password = TestApi.Password })).StatusCode);
        var detail = await client.GetStringAsync($"/api/platform/gym-onboardings/{result.GymId}");
        Assert.DoesNotContain("PasswordHash", detail); Assert.DoesNotContain(result.Token!, detail);
    }

    [Fact]
    public async Task Activation_IsSingleUseBindsTenantAndPreservesLegacyAccess()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        var invite = await app.Provision(Request);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new { token = invite.Token, password = "a", gymId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invite.Token!, TestApi.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invite.Token!, "another-password"))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Request.AdminEmail, password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var human = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(invite.GymId.ToString(), human.GetProperty("gymId").GetString());
        client.DefaultRequestHeaders.Authorization = new("Bearer", human.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/gym-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/platform/gym-onboardings")).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.GymOnboardingAuditLogs.CountAsync());
        Assert.NotNull((await db.AdminActivationInvitations.SingleAsync()).ConsumedAt);
    }

    [Fact]
    public async Task Invitations_ExpireExactlyAfter24HoursAndReissueRevokesOldWithoutDuplicateTenant()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        var invite = await app.Provision(Request);
        app.Clock.Now = app.Clock.Now.AddHours(24);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invite.Token!, TestApi.Password))).StatusCode);
        var replacement = await app.InScope(service => service.ReissueAsync(TestApi.OperatorId, invite.GymId, 1));
        Assert.NotNull(replacement);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invite.Token!, TestApi.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(replacement!.Token!, TestApi.Password))).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(db.GymOnboardings); Assert.Single(db.Users);
        Assert.Equal(2, await db.AdminActivationInvitations.CountAsync());
    }

    [Fact]
    public async Task Onboarding_IsResumableVersionedAndPhysicalValidationNeverAutomatic()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        var invite = await app.Provision(Request with { PhysicalAccessContracted = true });
        client.DefaultRequestHeaders.Authorization = new("Bearer", await app.OperatorToken(client));
        var path = $"/api/platform/gym-onboardings/{invite.GymId}/steps";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(1, "complete", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(1, "configuration", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(1, "configuration", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/onboarding/admin-activation", new ActivateAdminRequest(invite.Token!, TestApi.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(3, "complete", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(3, "physical", " "))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(3, "physical", "Synthetic test evidence only"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(path, new OnboardingStepRequest(4, "complete", null))).StatusCode);
        var detail = await client.GetFromJsonAsync<OnboardingDetail>($"/api/platform/gym-onboardings/{invite.GymId}");
        Assert.Equal("completed", detail!.Academy.Status); Assert.Equal(5, detail.Audit.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/platform/gym-onboardings?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Provision_RejectsUnknownFieldsAndNeverAcceptsClientGymId()
    {
        await using var app = new TestApi(); using var client = app.CreateClient(); app.Initialize();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await app.OperatorToken(client));
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/platform/gym-onboardings", new {
            Request.AdministrativeName, Request.TimeZoneId, Request.AdminName, Request.AdminEmail,
            gymId = Guid.NewGuid(), erpEnabled = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Provision(client, Guid.NewGuid().ToString(), Request with { TimeZoneId = "not-a-zone" })).StatusCode);
        using var scope = app.Services.CreateScope(); Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().GymOnboardings);
    }

    [Fact]
    public async Task AtomicRollback_WhenAuditSaveFailsLeavesNoUserSettingsInvitationOrTenant()
    {
        await using var app = new TestApi(); app.Initialize();
        app.FailNextSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Provision(Request));
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.Users); Assert.Empty(db.GymSettings); Assert.Empty(db.GymOnboardings);
        Assert.Empty(db.AdminActivationInvitations); Assert.Empty(db.GymOnboardingAuditLogs);
    }

    private static Task<HttpResponseMessage> Provision(HttpClient client, string key, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/platform/gym-onboardings") { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(message);
    }

    public sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public sealed class TestApi(bool postgres = false) : WebApplicationFactory<Program>
    {
        public const string Email = "operator77@example.test", Password = "synthetic-password-77";
        public const string Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        public static readonly Guid OperatorId = Guid.Parse("00000000-0000-4000-8000-000000000077");
        public TestClock Clock { get; } = new();
        public bool FailNextSave { get; set; }
        public Func<DbContext, Task>? BeforeSave { get; set; }
        private readonly SqliteConnection sqlite = new("Data Source=:memory:");
        private readonly string mfaKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        private string? adminConnection, testDatabase;
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["environment"] = "Testing", ["Jwt:Key"] = "synthetic-academy-key-01234567890123456789",
                ["Jwt:Issuer"] = "academy-tests", ["Jwt:Audience"] = "academy-tests", ["Jwt:ExpirationMinutes"] = "30",
                ["ConnectionStrings:DefaultConnection"] = "Host=unused.invalid;Database=unused",
                ["StudentLifecycle:Enabled"] = "false", ["Platform:Enabled"] = "true",
                ["Platform:Jwt:Key"] = "synthetic-platform-key-01234567890123456789", ["Platform:MfaEncryptionKey"] = mfaKey
            }));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); builder.ConfigureLogging(log => log.ClearProviders());
            builder.ConfigureServices(services => {
                services.RemoveAll<AppDbContext>(); services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock);
                if (postgres)
                {
                    var isolated = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES"));
                    if (isolated.Host != "127.0.0.1" || isolated.Port != 55439 || isolated.Database != "gymflow_codex_test_base" || isolated.Username != "gymflow_test")
                        throw new InvalidOperationException("Only the verified disposable test cluster is allowed.");
                    isolated.Pooling = false; adminConnection = isolated.ConnectionString;
                    testDatabase = $"gymflow_platform77_{Guid.NewGuid():N}";
                    using var connection = new NpgsqlConnection(adminConnection); connection.Open();
                    using var create = new NpgsqlCommand($"CREATE DATABASE \"{testDatabase}\"", connection); create.ExecuteNonQuery();
                    isolated.Database = testDatabase;
                    services.AddDbContext<AppDbContext>(o => o.UseNpgsql(isolated.ConnectionString).AddInterceptors(new SaveInterceptor(this)));
                }
                else { sqlite.Open(); services.AddDbContext<AppDbContext>(o => o.UseSqlite(sqlite).AddInterceptors(new SaveInterceptor(this))); }
            });
        }
        public void Initialize(bool seedOperator = true)
        {
            using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (postgres) db.Database.Migrate(); else db.Database.EnsureCreated();
            if (!seedOperator) return;
            if (db.PlatformOperators.Any()) return;
            db.PlatformOperators.Add(new PlatformOperator { Id = OperatorId, Email = Email, Name = "Operador sintético",
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password),
                ProtectedTotpSecret = scope.ServiceProvider.GetRequiredService<IPlatformSecurity>().ProtectSecret(Secret), CreatedAt = Clock.Now.UtcDateTime });
            db.SaveChanges();
        }
        public Task<HttpResponseMessage> Login(HttpClient client) => client.PostAsJsonAsync("/api/platform/auth/login",
            new PlatformLoginRequest(Email, Password, PlatformTotp.Generate(Secret, Clock.Now.ToUnixTimeSeconds() / 30)));
        public async Task<string> OperatorToken(HttpClient client)
        {
            var response = await Login(client); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<PlatformLoginResponse>())!.Token;
        }
        public Task<InvitationResult> Provision(ProvisionGymRequest request, string? key = null) =>
            InScope(service => service.ProvisionAsync(OperatorId, key ?? Guid.NewGuid().ToString(), request));
        public async Task<T> InScope<T>(Func<PlatformOnboardingService, Task<T>> action)
        {
            using var scope = Services.CreateScope(); return await action(scope.ServiceProvider.GetRequiredService<PlatformOnboardingService>());
        }
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync(); await sqlite.DisposeAsync();
            if (adminConnection is not null && testDatabase is not null)
            {
                await using var connection = new NpgsqlConnection(adminConnection); await connection.OpenAsync();
                // Only the database generated and owned by this fixture is removed.
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{testDatabase}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync();
            }
        }
        private sealed class SaveInterceptor(TestApi app) : SaveChangesInterceptor
        {
            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (app.FailNextSave && e.Context!.ChangeTracker.Entries<GymOnboardingAuditLog>().Any())
                { app.FailNextSave = false; throw new InvalidOperationException("Synthetic audit failure"); }
                if (app.BeforeSave is not null && e.Context!.ChangeTracker.Entries<GymOnboardingAuditLog>().Any())
                    await app.BeforeSave(e.Context);
                return result;
            }
        }
    }
}
