using System.Diagnostics;
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

// Disposable database only. Password reaches child processes on stdin, never
// in arguments, environment variables, or logs. No Production mode exists.
try
{
    if (args.Length == 1 && args[0] == "--run-local-concurrency") return await ParentAsync();
    if (args.Length == 5 && args[0] == "--worker-local-concurrency") return await WorkerAsync(args);
    Console.Error.WriteLine("Usage: GymFlow.PlatformProvisioner.Concurrency --run-local-concurrency");
    return 2;
}
catch
{
    Console.Error.WriteLine("FAIL: TEST_SETUP_OR_PROCESS_ERROR; database retained for inspection.");
    return 1;
}

static async Task<int> ParentAsync()
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
        throw new InvalidOperationException("Interactive console required.");
    var host = Ask("Local host (127.0.0.1 or ::1): ").Trim();
    var portText = Ask("Local port: ").Trim();
    var runId = Ask("New disposable run ID (32 lowercase hex): ").Trim();
    var user = Ask("Local database user: ").Trim();
    ValidateTarget(host, portText, runId, user);
    var dbName = DatabaseName(runId);
    if (Ask($"Confirm exact destination {host}:{portText}/{dbName}: ") != $"{host}:{portText}/{dbName}")
        throw new InvalidOperationException("Target not confirmed.");
    var password = AskHidden("Local database password (hidden): ");
    if (password.Length == 0) throw new InvalidOperationException("Password required.");
    await using var connection = NewConnection(host, int.Parse(portText), dbName, user, password);
    await connection.OpenAsync();
    await VerifyDatabaseAsync(connection, host, int.Parse(portText), dbName, user, runId, expectedTables: 0);
    if (Ask("Type CREATE DISPOSABLE TEST TABLE: ") != "CREATE DISPOSABLE TEST TABLE")
        throw new InvalidOperationException("Creation not confirmed.");
    await using (var ddl = new NpgsqlCommand(@"
CREATE TABLE ""PlatformOperators"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Email"" character varying(200) NOT NULL,
    ""Name"" character varying(150) NOT NULL,
    ""PasswordHash"" text NOT NULL,
    ""ProtectedTotpSecret"" text NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""CredentialVersion"" bigint NOT NULL,
    ""LastTotpStep"" bigint NOT NULL,
    ""FailedAttempts"" integer NOT NULL,
    ""LockedUntil"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX ""IX_PlatformOperators_Email"" ON ""PlatformOperators"" (""Email"");", connection))
        await ddl.ExecuteNonQueryAsync();

    // Block both children on the real writer's transaction lock until both
    // processes pass the target preflight and are ready to attempt insertion.
    await using (var hold = new NpgsqlCommand("SELECT pg_advisory_lock(131, 1)", connection))
        await hold.ExecuteNonQueryAsync();
    Process? first = null, second = null;
    try
    {
        first = StartWorker(host, portText, runId, user, password);
        second = StartWorker(host, portText, runId, user, password);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        if (await first.StandardOutput.ReadLineAsync(timeout.Token) != "READY" ||
            await second.StandardOutput.ReadLineAsync(timeout.Token) != "READY")
            throw new InvalidOperationException("Worker preflight failed.");
        await first.StandardInput.WriteLineAsync("GO");
        await second.StandardInput.WriteLineAsync("GO");
        await Task.Delay(1000, timeout.Token);
        await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(131, 1)", connection))
            await release.ExecuteNonQueryAsync(timeout.Token);
        await Task.WhenAll(first.WaitForExitAsync(timeout.Token), second.WaitForExitAsync(timeout.Token));
        var exits = new[] { first.ExitCode, second.ExitCode };
        await using var count = new NpgsqlCommand("SELECT count(*) FROM \"PlatformOperators\"", connection);
        var rows = Convert.ToInt64(await count.ExecuteScalarAsync());
        var pass = rows == 1 && exits.Count(x => x == 0) == 1 && exits.Count(x => x is 3 or 4) == 1;
        Console.WriteLine($"{(pass ? "PASS" : "FAIL")}: rows={rows}; worker_exit_codes={exits[0]},{exits[1]}; database retained.");
        return pass ? 0 : 1;
    }
    finally
    {
        if (first is { HasExited: false }) first.Kill(entireProcessTree: true);
        if (second is { HasExited: false }) second.Kill(entireProcessTree: true);
        first?.Dispose(); second?.Dispose();
    }
}

static Process StartWorker(string host, string port, string runId, string user, string password)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false, RedirectStandardInput = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var arg in new[] { "--worker-local-concurrency", host, port, runId, user }) start.ArgumentList.Add(arg);
    var process = Process.Start(start) ?? throw new InvalidOperationException("Worker did not start.");
    process.StandardInput.WriteLine(password);
    return process;
}

static async Task<int> WorkerAsync(string[] args)
{
    var host = args[1]; var portText = args[2]; var runId = args[3]; var user = args[4];
    ValidateTarget(host, portText, runId, user);
    var password = await Console.In.ReadLineAsync() ?? throw new InvalidOperationException("Password missing.");
    await using var connection = NewConnection(host, int.Parse(portText), DatabaseName(runId), user, password);
    await connection.OpenAsync();
    await VerifyDatabaseAsync(connection, host, int.Parse(portText), DatabaseName(runId), user, runId, expectedTables: 1);
    Console.WriteLine("READY");
    await Console.Out.FlushAsync();
    if (await Console.In.ReadLineAsync() != "GO") return 2;
    var secret = PlatformTotp.NewSecret();
    var key = RandomNumberGenerator.GetBytes(32);
    string protectedSecret;
    try { protectedSecret = new PlatformSecurity(new PlatformAuthOptions { MfaEncryptionKey = key }).ProtectSecret(secret); }
    finally { CryptographicOperations.ZeroMemory(key); }
    var actor = new PlatformOperator
    {
        Id = Guid.NewGuid(), Name = "Synthetic concurrency operator",
        Email = $"synthetic-{Guid.NewGuid():N}@example.invalid",
        PasswordHash = new PasswordHasher().Hash(Guid.NewGuid().ToString("N")),
        ProtectedTotpSecret = protectedSecret, CreatedAt = DateTime.UtcNow
    };
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    try { return await FirstOperatorWriter.TryCreateAsync(db, actor) ? 0 : 3; }
    catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
    { return 4; }
}

static async Task VerifyDatabaseAsync(NpgsqlConnection connection, string host, int port,
    string dbName, string user, string runId, long expectedTables)
{
    await using var check = new NpgsqlCommand(@"
SELECT current_database(), current_user, inet_server_port(), host(inet_server_addr()),
       (SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database()),
       (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname NOT IN ('pg_catalog','information_schema')
          AND n.nspname NOT LIKE 'pg_toast%' AND c.relkind IN ('r','p'))", connection);
    await using var row = await check.ExecuteReaderAsync();
    if (!await row.ReadAsync() || row.GetString(0) != dbName || row.GetString(1) != user ||
        row.GetInt32(2) != port || row.GetString(3) != host ||
        row.GetString(4) != $"ISSUE131_PLATFORM_DISPOSABLE:{runId}" || row.GetInt64(5) != expectedTables)
        throw new InvalidOperationException("Disposable target identity or table count mismatch.");
}

static void ValidateTarget(string host, string port, string runId, string user)
{
    if (host is not ("127.0.0.1" or "::1") ||
        !int.TryParse(port, out var parsed) || parsed is < 1 or > 65535 ||
        !Regex.IsMatch(runId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant) ||
        !Regex.IsMatch(user, "^[A-Za-z0-9_.-]{1,100}$", RegexOptions.CultureInvariant))
        throw new InvalidOperationException("Invalid local disposable target.");
}

static string DatabaseName(string runId) => $"issue131_platform_test_{runId}";
static NpgsqlConnection NewConnection(string host, int port, string db, string user, string password) =>
    new(new NpgsqlConnectionStringBuilder
    {
        Host = host, Port = port, Database = db, Username = user, Password = password,
        SslMode = SslMode.Disable, Pooling = false, Timeout = 10, CommandTimeout = 10,
        IncludeErrorDetail = false, LogParameters = false
    }.ConnectionString);
static string Ask(string prompt) { Console.Write(prompt); return Console.ReadLine() ?? throw new InvalidOperationException("Input ended."); }
static string AskHidden(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar) && value.Length < 256) value.Append(key.KeyChar);
    }
}
