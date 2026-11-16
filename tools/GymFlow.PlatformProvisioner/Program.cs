using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GymFlow.Api.Platform;
using GymFlow.Application.Security;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Security;
using GymFlow.PlatformProvisioner;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Deliberately separate from the deployed API. No configuration files, user
// secrets, environment connection strings, or command-line credentials are read.
if (args.Length != 1 || args[0] is not ("--provision-production-platform-operator" or "--test-local-disposable"))
{
    Console.Error.WriteLine("Usage: GymFlow.PlatformProvisioner --test-local-disposable | --provision-production-platform-operator");
    return 2;
}
if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.Error.WriteLine("FAIL: INTERACTIVE_CONSOLE_REQUIRED");
    return 2;
}

byte[]? mfaKey = null;
try
{
    var localTest = args[0] == "--test-local-disposable";
    string host;
    string database;
    string? testRunId = null;
    int port;
    if (localTest)
    {
        host = ReadVisible("Local PostgreSQL host (127.0.0.1 or ::1): ").Trim();
        if (host is not ("127.0.0.1" or "::1"))
            throw new ProvisioningFailure("LOCAL_LOOPBACK_REQUIRED");
        if (!int.TryParse(ReadVisible("Local PostgreSQL port: "), out port) || port is < 1 or > 65535)
            throw new ProvisioningFailure("INVALID_LOCAL_PORT");
        testRunId = ReadVisible("Disposable test run ID (32 lowercase hex characters): ").Trim();
        if (!Regex.IsMatch(testRunId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant))
            throw new ProvisioningFailure("INVALID_TEST_RUN_ID");
        database = $"issue131_platform_test_{testRunId}";
    }
    else
    {
        var projectRef = ReadVisible("Supabase Production project ref: ");
        if (projectRef != "tkvlpiczyoqcofbwgeyg")
            throw new ProvisioningFailure("PRODUCTION_PROJECT_REF_MISMATCH");
        host = $"db.{projectRef}.supabase.co";
        port = 5432;
        database = ReadVisible("Database name: ");
    }
    var username = ReadVisible("Database user: ");
    if (!Regex.IsMatch(database, "^[A-Za-z0-9_]{1,63}$", RegexOptions.CultureInvariant) ||
        !Regex.IsMatch(username, "^[A-Za-z0-9_.-]{1,100}$", RegexOptions.CultureInvariant))
        throw new ProvisioningFailure("INVALID_DATABASE_IDENTITY");

    Console.WriteLine($"Target: {host}:{port}/{database}, user {username}, {(localTest ? "local TCP only" : "TLS verify-full")}.");
    RequirePhrase("Type the exact target host/port/database to continue: ", $"{host}:{port}/{database}");
    var password = ReadHidden("Database password (hidden): ");
    if (password.Length == 0) throw new ProvisioningFailure("MISSING_DATABASE_PASSWORD");
    var cs = new NpgsqlConnectionStringBuilder
    {
        Host = host, Port = port, Database = database, Username = username,
        Password = password, SslMode = localTest ? SslMode.Disable : SslMode.VerifyFull,
        Pooling = false, Timeout = 10,
        CommandTimeout = 10, IncludeErrorDetail = false, LogParameters = false
    };
    password = string.Empty;
    await using var connection = new NpgsqlConnection(cs.ConnectionString);
    cs.Password = string.Empty;
    await connection.OpenAsync();
    await using (var identity = new NpgsqlCommand(
        "SELECT current_database(), current_user, inet_server_port(), host(inet_server_addr())", connection))
    await using (var reader = await identity.ExecuteReaderAsync())
    {
        if (!await reader.ReadAsync() || reader.GetString(0) != database ||
            reader.GetString(1) != username || reader.GetInt32(2) != port ||
            (localTest && reader.GetString(3) is not ("127.0.0.1" or "::1")))
            throw new ProvisioningFailure("DATABASE_IDENTITY_MISMATCH");
    }
    if (localTest)
    {
        // The marker must be placed by a separately controlled fixture setup;
        // a familiar database name alone never authorizes a test write.
        await using var marker = new NpgsqlCommand(
            "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database()", connection);
        if (await marker.ExecuteScalarAsync() is not string comment ||
            comment != $"ISSUE131_PLATFORM_DISPOSABLE:{testRunId}")
            throw new ProvisioningFailure("DISPOSABLE_DATABASE_MARKER_MISSING");
    }
    await using (var migration = new NpgsqlCommand(
        "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1", connection))
    {
        if (await migration.ExecuteScalarAsync() is not string latest ||
            latest != "20261113003407_AddGymMaintenanceStateAndAudit")
            throw new ProvisioningFailure("MIGRATION_BASELINE_MISMATCH");
    }
    Console.WriteLine("Database identity and required migration: confirmed. Existing operator check follows.");

    var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
    await using var db = new AppDbContext(options);
    if (await db.PlatformOperators.AsNoTracking().AnyAsync())
        throw new ProvisioningFailure("OPERATOR_ALREADY_EXISTS");

    var name = ReadVisible("Operator name: ").Trim();
    var email = ReadVisible("Operator email: ").Trim().ToLowerInvariant();
    if (name.Length is 0 or > 150 || email.Length > 200 ||
        !new EmailAddressAttribute().IsValid(email))
        throw new ProvisioningFailure("INVALID_OPERATOR_IDENTITY");
    var operatorPassword = ReadHidden("New operator password (hidden): ");
    PasswordPolicy.Validate(operatorPassword);
    if (ReadHidden("Confirm operator password (hidden): ") != operatorPassword)
        throw new ProvisioningFailure("PASSWORD_MISMATCH");

    if (localTest)
        mfaKey = RandomNumberGenerator.GetBytes(32);
    else
    {
        var encodedKey = ReadHidden("Production Platform MFA encryption key, Base64 (hidden): ");
        if (ReadHidden("Confirm Platform MFA encryption key (hidden): ") != encodedKey)
            throw new ProvisioningFailure("MFA_ENCRYPTION_KEY_MISMATCH");
        try { mfaKey = Convert.FromBase64String(encodedKey); }
        catch (FormatException) { throw new ProvisioningFailure("INVALID_MFA_ENCRYPTION_KEY"); }
        encodedKey = string.Empty;
        if (mfaKey.Length != 32) throw new ProvisioningFailure("INVALID_MFA_ENCRYPTION_KEY");
    }

    var secret = PlatformTotp.NewSecret();
    Console.WriteLine("Register this one-time TOTP secret in the authenticator. Keep this console private; no transcript or recording:");
    Console.WriteLine(secret);
    Console.Write("Authenticator code (hidden): ");
    var code = ReadHidden();
    var step = PlatformTotp.Validate(secret, code, DateTimeOffset.UtcNow, -1);
    if (step is null) throw new ProvisioningFailure("MFA_NOT_CONFIRMED");
    var confirmation = localTest ? "PROVISION ONE DISPOSABLE TEST OPERATOR" : "PROVISION ONE PRODUCTION OPERATOR";
    RequirePhrase($"Final confirmation (type {confirmation}): ", confirmation);

    var security = new PlatformSecurity(new PlatformAuthOptions { MfaEncryptionKey = mfaKey });
    var protectedSecret = security.ProtectSecret(secret);
    if (security.ValidateTotp(protectedSecret, code, DateTimeOffset.UtcNow, -1) != step)
        throw new ProvisioningFailure("MFA_PROTECTION_CHECK_FAILED");
    var actor = new PlatformOperator
    {
        Id = Guid.NewGuid(), Name = name, Email = email,
        PasswordHash = new PasswordHasher().Hash(operatorPassword),
        ProtectedTotpSecret = protectedSecret,
        LastTotpStep = step.Value, CreatedAt = DateTime.UtcNow
    };
    if (!await FirstOperatorWriter.TryCreateAsync(db, actor))
        throw new ProvisioningFailure("OPERATOR_ALREADY_EXISTS");
    Console.WriteLine($"PASS: one {(localTest ? "disposable test" : "Production")} PlatformOperator provisioned; id={actor.Id}. No API was started.");
    return 0;
}
catch (ProvisioningFailure ex)
{
    Console.Error.WriteLine($"FAIL: {ex.Code}; no provisioned operator confirmed.");
    return 1;
}
catch (Exception)
{
    // Driver exceptions may contain connection details. Never emit raw errors.
    Console.Error.WriteLine("FAIL: DATABASE_OR_PROCESS_ERROR; inspect state before any retry.");
    return 1;
}
finally
{
    if (mfaKey is not null) CryptographicOperations.ZeroMemory(mfaKey);
}

static string ReadVisible(string prompt)
{
    Console.Write(prompt);
    return Console.ReadLine() ?? throw new ProvisioningFailure("CONSOLE_INPUT_ENDED");
}

static string ReadHidden(string? prompt = null)
{
    if (prompt is not null) Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar) && value.Length < 256) value.Append(key.KeyChar);
    }
}

static void RequirePhrase(string prompt, string expected)
{
    if (ReadVisible(prompt) != expected) throw new ProvisioningFailure("CONFIRMATION_MISMATCH");
}

sealed class ProvisioningFailure(string code) : Exception
{
    public string Code { get; } = code;
}
