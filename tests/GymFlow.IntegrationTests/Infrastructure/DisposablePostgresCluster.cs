using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Npgsql;

namespace GymFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Starts and owns one loopback-only PostgreSQL cluster in a private temporary
/// directory for the lifetime of the test host. Existing server settings are
/// treated only as an opt-in and are never used as a connection destination.
/// </summary>
internal static class DisposablePostgresCluster
{
    private const string StandardDatabase = "gymflow_codex_test_base";
    private const string StandardUser = "gymflow_test";
    private const string CancellationDatabase = "gymflow_issue130_cancel_test_base";
    private const string CancellationUser = "gymflow_cancel_test";
    private const string RecoveryDatabase = "issue130_restore_avelri";
    private const string WorkDatabase = "issue130_erp_work_20261101";
    private const string ProtectedDataRoot = @"R:\Issue130";
    private const string OwnerMarkerName = ".gymflow-disposable-postgres-owner";
    private const string HarnessApplicationName = "GymFlow.DisposableTestHarness";
    private const int ToolProcessTimeoutMilliseconds = 120_000;
    private const int ToolTerminationWaitMilliseconds = 5_000;
    private const int ToolStreamDrainTimeoutMilliseconds = 5_000;
    private const int MaximumToolDiagnosticCharacters = 4_000;

    private static readonly HashSet<int> ReservedPorts = [5432, 49357, 55439, 55442, 55444];
    private static readonly object Sync = new();
    private static OwnedCluster? _owned;
    private static bool _processExitRegistered;

    public static NpgsqlConnectionStringBuilder BuildConnectionString(
        string connectionEnvironmentVariable,
        string portEnvironmentVariable,
        string expectedDatabase,
        string expectedUser,
        string dataDirectoryEnvironmentVariable)
    {
        var configuredConnection = Environment.GetEnvironmentVariable(connectionEnvironmentVariable);
        var configuredPort = Environment.GetEnvironmentVariable(portEnvironmentVariable);
        var configuredDataDirectory = string.IsNullOrWhiteSpace(dataDirectoryEnvironmentVariable)
            ? null
            : Environment.GetEnvironmentVariable(dataDirectoryEnvironmentVariable);
        ValidateOptInTarget(
            configuredConnection,
            configuredPort,
            configuredDataDirectory,
            expectedDatabase,
            expectedUser);

        var owned = GetOrStartOwnedCluster();
        if (!IsAllowedIdentity(expectedDatabase, expectedUser))
        {
            throw new InvalidOperationException("The requested PostgreSQL test identity is not approved.");
        }

        EnsureOwnedCluster(owned);
        return CreateConnectionString(owned, expectedDatabase, expectedUser);
    }

    public static async Task VerifyIdentityAsync(
        NpgsqlConnection connection,
        string expectedDatabase,
        string expectedUser,
        int expectedPort,
        string dataDirectoryEnvironmentVariable)
    {
        var owned = RequireOwnedCluster();
        EnsureOwnedCluster(owned);
        ValidateConnectionForOwnedCluster(connection, owned, expectedDatabase, expectedUser, expectedPort);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException("The owned PostgreSQL connection must be open before identity verification.");
        }

        var configuredDataDirectory = Environment.GetEnvironmentVariable(dataDirectoryEnvironmentVariable);
        if (IsProtectedDataDirectory(configuredDataDirectory))
        {
            throw new InvalidOperationException("The protected Issue #130 PostgreSQL data directory is not allowed.");
        }

        await VerifyBackendIdentityAsync(connection, owned, expectedDatabase, expectedUser);
    }

    public static async Task VerifyOwnedConnectionAsync(NpgsqlConnection connection)
    {
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        await OpenVerifiedConnectionAsync(connection);
        if (openedHere)
        {
            await connection.CloseAsync();
        }
    }

    public static async Task OpenVerifiedConnectionAsync(NpgsqlConnection connection)
    {
        var owned = RequireOwnedCluster();
        EnsureOwnedCluster(owned);
        var requested = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        var expectedDatabase = requested.Database ??
            throw new InvalidOperationException("The owned PostgreSQL connection has no database identity.");
        var expectedUser = requested.Username ??
            throw new InvalidOperationException("The owned PostgreSQL connection has no user identity.");
        ValidateConnectionForOwnedCluster(connection, owned, expectedDatabase, expectedUser, owned.Port);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        try
        {
            await VerifyIdentityAsync(connection, expectedDatabase, expectedUser, owned.Port, string.Empty);
        }
        catch
        {
            await connection.CloseAsync();
            throw;
        }
    }

    public static string NewDatabaseName(string prefix)
    {
        var owned = RequireOwnedCluster();
        EnsureOwnedCluster(owned);
        if (!AllowedDatabasePrefixes.Contains(prefix, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The requested database prefix is not approved for this harness.");
        }

        var name = $"{prefix}{Guid.NewGuid():N}";
        if (!owned.ReservedDatabases.TryAdd(name, 0))
        {
            throw new InvalidOperationException("The generated test database name was already reserved.");
        }
        return name;
    }

    public static async Task CreateDatabaseAsync(NpgsqlConnection adminConnection, string databaseName)
    {
        var owned = RequireOwnedCluster();
        await VerifyOwnedConnectionAsync(adminConnection);
        ValidateGeneratedDatabaseName(databaseName);

        if (!owned.ReservedDatabases.ContainsKey(databaseName) || owned.CreatedDatabases.ContainsKey(databaseName))
        {
            throw new InvalidOperationException("The test database name was not uniquely reserved by this harness run.");
        }

        await using (var exists = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", adminConnection))
        {
            exists.Parameters.AddWithValue("name", databaseName);
            if ((bool)(await exists.ExecuteScalarAsync())!)
            {
                throw new InvalidOperationException("The generated test database already exists; no database was changed.");
            }
        }

        EnsureOwnedCluster(owned);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", adminConnection);
        await create.ExecuteNonQueryAsync();
        if (!owned.CreatedDatabases.TryAdd(databaseName, 0) || !owned.ReservedDatabases.TryRemove(databaseName, out _))
        {
            throw new InvalidOperationException("The created test database could not be registered to this harness run.");
        }
    }

    public static async Task DropDatabaseAsync(
        NpgsqlConnection adminConnection,
        string databaseName,
        bool ifExists = false)
    {
        var owned = RequireOwnedCluster();
        await VerifyOwnedConnectionAsync(adminConnection);
        ValidateGeneratedDatabaseName(databaseName);

        if (!owned.CreatedDatabases.ContainsKey(databaseName))
        {
            if (ifExists)
            {
                return;
            }

            throw new InvalidOperationException("The harness will not drop a database it did not create in this run.");
        }

        var currentDatabase = new NpgsqlConnectionStringBuilder(adminConnection.ConnectionString).Database;
        if (string.Equals(currentDatabase, databaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The harness will not drop the database used by its admin connection.");
        }

        EnsureOwnedCluster(owned);
        var ifExistsClause = ifExists ? "IF EXISTS " : string.Empty;
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE {ifExistsClause}\"{databaseName}\" WITH (FORCE)", adminConnection);
        await drop.ExecuteNonQueryAsync();
        owned.CreatedDatabases.TryRemove(databaseName, out _);
    }

    public static async Task RunWithVerifiedConnectionAsync(
        NpgsqlConnection connection,
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var owned = RequireOwnedCluster();
        EnsureOwnedCluster(owned);
        var requested = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        var expectedDatabase = requested.Database ??
            throw new InvalidOperationException("The owned PostgreSQL connection has no database identity.");
        var expectedUser = requested.Username ??
            throw new InvalidOperationException("The owned PostgreSQL connection has no user identity.");
        ValidateConnectionForOwnedCluster(connection, owned, expectedDatabase, expectedUser, owned.Port);
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        await OpenVerifiedConnectionAsync(connection);

        try
        {
            EnsureOwnedCluster(RequireOwnedCluster());
            await operation();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static bool IsReservedPort(int port) => ReservedPorts.Contains(port);

    private static readonly string[] AllowedDatabasePrefixes =
    [
        "gymflow_codex_test_dashboard_",
        "gymflow_platform77_",
        "gymflow_codex_test_pin_",
        "gymflow_codex_test_",
        "gymflow_cancel_test_"
    ];

    internal static bool Postgres17ToolsAvailable()
    {
        try
        {
            var bin = FindPostgresBinDirectory();
            return File.Exists(Path.Combine(bin, OperatingSystem.IsWindows() ? "initdb.exe" : "initdb")) &&
                   File.Exists(Path.Combine(bin, OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl")) &&
                   File.Exists(Path.Combine(bin, OperatingSystem.IsWindows() ? "postgres.exe" : "postgres"));
        }
        catch
        {
            return false;
        }
    }

    internal static (int Port, string DataDirectory) CurrentOwnedIdentityForTests()
    {
        var owner = RequireOwnedCluster();
        EnsureOwnedCluster(owner);
        return (owner.Port, owner.DataDirectory);
    }

    internal static bool IsProtectedDatabase(string? databaseName) =>
        string.Equals(databaseName, RecoveryDatabase, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(databaseName, WorkDatabase, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(databaseName, "gymflow_dev", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(databaseName, "gymflow_issue130_manual_20261026", StringComparison.OrdinalIgnoreCase);

    internal static bool IsProtectedDataDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim().TrimEnd('\\', '/');
        return trimmed.Equals("R:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("R:\\", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("R:/", StringComparison.OrdinalIgnoreCase) ||
               IsWithinPath(trimmed, ProtectedDataRoot);
    }

    internal static void ValidateOptInTarget(
        string? configuredConnection,
        string? configuredPort,
        string? configuredDataDirectory,
        string expectedDatabase,
        string expectedUser)
    {
        if (string.IsNullOrWhiteSpace(configuredConnection) ||
            !int.TryParse(configuredPort, out var requestedPort) ||
            requestedPort is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(configuredDataDirectory) ||
            !Path.IsPathFullyQualified(configuredDataDirectory))
        {
            throw new InvalidOperationException("Explicit local PostgreSQL test opt-in settings are required.");
        }

        NpgsqlConnectionStringBuilder configured;
        try
        {
            configured = new NpgsqlConnectionStringBuilder(configuredConnection);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The PostgreSQL test opt-in settings are invalid.");
        }

        var allowedKeys = new HashSet<string>(
            ["Host", "Port", "Database", "Username", "Password", "SSL Mode"],
            StringComparer.OrdinalIgnoreCase);
        if (configured.Keys.Cast<string>().Any(key => !allowedKeys.Contains(key)) ||
            configured.Host != "127.0.0.1" ||
            configured.Port != requestedPort ||
            configured.Database != expectedDatabase ||
            configured.Username != expectedUser ||
            string.IsNullOrEmpty(configured.Password) ||
            configured.SslMode != SslMode.Disable)
        {
            throw new InvalidOperationException("The PostgreSQL test opt-in settings do not match the approved local test identity.");
        }

        if (requestedPort == 49357 || IsProtectedDatabase(configured.Database) ||
            IsProtectedDataDirectory(configuredDataDirectory))
        {
            throw new InvalidOperationException("The Issue #130 recovery and work PostgreSQL targets are prohibited.");
        }

        if (!IsAllowedIdentity(expectedDatabase, expectedUser))
        {
            throw new InvalidOperationException("The requested PostgreSQL test identity is not approved.");
        }
    }

    private static bool IsAllowedIdentity(string database, string user) =>
        (database == StandardDatabase && user == StandardUser) ||
        (database == CancellationDatabase && user == CancellationUser);

    private static void ValidateGeneratedDatabaseName(string databaseName)
    {
        if (IsProtectedDatabase(databaseName) ||
            !AllowedDatabasePrefixes.Any(prefix => databaseName.StartsWith(prefix, StringComparison.Ordinal) &&
                System.Text.RegularExpressions.Regex.IsMatch(
                    databaseName[prefix.Length..],
                    "^[0-9a-f]{32}$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
        {
            throw new InvalidOperationException("Only a database name generated for this test harness is allowed.");
        }
    }

    private static OwnedCluster GetOrStartOwnedCluster()
    {
        lock (Sync)
        {
            if (_owned is not null)
            {
                EnsureOwnedCluster(_owned);
                return _owned;
            }

            var owner = StartOwnedCluster();
            _owned = owner;
            try
            {
                InitializeBaseDatabases(owner);
                if (!_processExitRegistered)
                {
                    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                    _processExitRegistered = true;
                }

                return owner;
            }
            catch
            {
                _owned = null;
                StopAndRemoveOwnedCluster(owner);
                throw;
            }
        }
    }

    private static OwnedCluster StartOwnedCluster()
    {
        if (OperatingSystem.IsWindows() &&
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException("PostgreSQL test clusters must be started under a non-administrative Windows token.");
        }

        var binDirectory = FindPostgresBinDirectory();
        var initdb = Path.Combine(binDirectory, OperatingSystem.IsWindows() ? "initdb.exe" : "initdb");
        var pgCtl = Path.Combine(binDirectory, OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl");
        var postgres = Path.Combine(binDirectory, OperatingSystem.IsWindows() ? "postgres.exe" : "postgres");
        if (!File.Exists(initdb) || !File.Exists(pgCtl) || !File.Exists(postgres))
        {
            throw new InvalidOperationException("PostgreSQL 17 initdb, pg_ctl, and postgres executables are required.");
        }

        var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (IsProtectedDataDirectory(tempRoot) || HasReparsePointInPath(tempRoot))
        {
            throw new InvalidOperationException("The system temporary directory is not an approved physical location.");
        }

        var runId = Guid.NewGuid().ToString("N");
        var clusterRoot = Path.Combine(tempRoot, $"GymFlow-PostgresTests-{runId}");
        if (Directory.Exists(clusterRoot) || File.Exists(clusterRoot))
        {
            throw new InvalidOperationException("The unique PostgreSQL test directory already exists; it will not be reused.");
        }

        Directory.CreateDirectory(clusterRoot);
        ApplyPrivateDirectoryPermissions(clusterRoot);
        var dataDirectory = Path.Combine(clusterRoot, "data");
        Directory.CreateDirectory(dataDirectory);
        ApplyPrivateDirectoryPermissions(dataDirectory);
        var markerPath = Path.Combine(clusterRoot, OwnerMarkerName);
        using (var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(marker, new UTF8Encoding(false)))
        {
            writer.Write(runId);
        }

        var passwordPath = Path.Combine(clusterRoot, ".initdb-password");
        var superuserPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var cancellationPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var port = ReservePort();
        var owner = new OwnedCluster(
            runId, tempRoot, clusterRoot, dataDirectory, markerPath, initdb, pgCtl, postgres,
            port, superuserPassword, cancellationPassword);

        try
        {
            WritePrivateFile(passwordPath, superuserPassword);
            RunTool(initdb, ["--pgdata", dataDirectory, "--username", StandardUser,
                "--pwfile", passwordPath, "--auth-local=scram-sha-256",
                "--auth-host=scram-sha-256", "--encoding=UTF8", "--no-instructions"], clusterRoot);
            File.Delete(passwordPath);

            var postgresLogPath = Path.Combine(clusterRoot, "postgres.log");
            RunTool(pgCtl, ["start", "--pgdata", dataDirectory, "--log", postgresLogPath,
                "--options", $"-h 127.0.0.1 -p {port} -c listen_addresses=127.0.0.1", "--wait", "--timeout", "30"],
                clusterRoot, redirectOutput: false, diagnosticLogPath: postgresLogPath);

            owner.PostgresProcessId = ReadAndValidatePostmasterPid(owner);
            ValidateOwnedServerStartup(owner);
            EnsureOwnedCluster(owner);
            using var bootstrap = new NpgsqlConnection(CreateConnectionString(owner, "postgres", StandardUser).ConnectionString);
            bootstrap.Open();
            VerifyBackendIdentityAsync(bootstrap, owner, "postgres", StandardUser).GetAwaiter().GetResult();

            EnsureDatabaseAbsent(bootstrap, StandardDatabase);
            EnsureOwnedCluster(owner);
            ExecuteDdl(bootstrap, $"CREATE DATABASE \"{StandardDatabase}\" OWNER \"{StandardUser}\"");

            EnsureDatabaseAbsent(bootstrap, CancellationDatabase);
            EnsureOwnedCluster(owner);
            ExecuteDdl(bootstrap,
                $"CREATE ROLE \"{CancellationUser}\" LOGIN CREATEDB PASSWORD '{cancellationPassword}'");
            ExecuteDdl(bootstrap,
                $"CREATE DATABASE \"{CancellationDatabase}\" OWNER \"{CancellationUser}\"");
            return owner;
        }
        catch
        {
            StopAndRemoveOwnedCluster(owner);
            throw;
        }
        finally
        {
            if (File.Exists(passwordPath))
            {
                File.Delete(passwordPath);
            }
        }
    }

    private static void InitializeBaseDatabases(OwnedCluster owner)
    {
        EnsureOwnedCluster(owner);
        using var admin = new NpgsqlConnection(CreateConnectionString(owner, "postgres", StandardUser).ConnectionString);
        admin.Open();
        VerifyBackendIdentityAsync(admin, owner, "postgres", StandardUser).GetAwaiter().GetResult();
        EnsureDatabaseExists(admin, StandardDatabase);
        EnsureDatabaseExists(admin, CancellationDatabase);
    }

    private static async Task VerifyBackendIdentityAsync(
        NpgsqlConnection connection,
        OwnedCluster owner,
        string expectedDatabase,
        string expectedUser)
    {
        EnsureOwnedCluster(owner);
        await using var command = new NpgsqlCommand("""
            SELECT current_database(), current_user, inet_server_addr()::text,
                   host(inet_server_addr()), family(inet_server_addr()), inet_server_port(),
                   current_setting('data_directory'), current_setting('server_version_num')::integer,
                   EXISTS (SELECT 1 FROM pg_database WHERE datname = 'gymflow_dev'),
                   EXISTS (SELECT 1 FROM pg_database WHERE datname = 'gymflow_issue130_manual_20261026'),
                   EXISTS (SELECT 1 FROM pg_database WHERE datname = 'issue130_restore_avelri'),
                   EXISTS (SELECT 1 FROM pg_database WHERE datname = 'issue130_erp_work_20261101')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("The owned PostgreSQL server identity could not be read.");
        }

        var serverAddressText = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        var serverAddressHost = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
        var serverAddressFamily = reader.IsDBNull(4) ? -1 : reader.GetInt32(4);
        var serverPort = reader.GetInt32(5);
        var failedChecks = new List<string>();
        if (reader.GetString(0) != expectedDatabase) failedChecks.Add("database");
        if (reader.GetString(1) != expectedUser) failedChecks.Add("user");
        var serverAddressParsed = IPAddress.TryParse(serverAddressHost, out var serverAddress);
        var addressFamilyMatches = serverAddressParsed &&
            serverAddressFamily == (serverAddress!.AddressFamily == AddressFamily.InterNetwork ? 4 : 6);
        var normalizedServerAddress = serverAddressParsed && serverAddress!.IsIPv4MappedToIPv6
            ? serverAddress.MapToIPv4()
            : serverAddress;
        if (!addressFamilyMatches || normalizedServerAddress is null || !IPAddress.IsLoopback(normalizedServerAddress))
            failedChecks.Add(
                $"loopback address (inet_server_addr={serverAddressText}; host={serverAddressHost}; " +
                $"family={serverAddressFamily}; port={serverPort})");
        if (serverPort != owner.Port) failedChecks.Add("port");
        if (!string.Equals(Path.GetFullPath(reader.GetString(6)).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(owner.DataDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            failedChecks.Add("owned temporary PGDATA");
        if (reader.GetInt32(7) is < 170000 or >= 180000) failedChecks.Add("PostgreSQL 17 version");
        if (reader.GetBoolean(8)) failedChecks.Add("gymflow_dev exclusion");
        if (reader.GetBoolean(9)) failedChecks.Add("manual database exclusion");
        if (reader.GetBoolean(10)) failedChecks.Add("Issue #130 recovery database exclusion");
        if (reader.GetBoolean(11)) failedChecks.Add("Issue #130 work database exclusion");
        if (failedChecks.Count > 0)
        {
            throw new InvalidOperationException(
                $"Owned PostgreSQL identity verification failed for: {string.Join(", ", failedChecks)}; no test operation was performed.");
        }
    }

    private static void ValidateConnectionForOwnedCluster(
        NpgsqlConnection connection,
        OwnedCluster owner,
        string expectedDatabase,
        string expectedUser,
        int expectedPort)
    {
        var requested = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        var allowedKeys = new HashSet<string>(
            ["Host", "Port", "Database", "Username", "Password", "Pooling", "SSL Mode",
                "Timeout", "Command Timeout", "Application Name"],
            StringComparer.OrdinalIgnoreCase);
        if (!IsAllowedIdentity(expectedDatabase, expectedUser) &&
            !owner.CreatedDatabases.ContainsKey(expectedDatabase))
        {
            throw new InvalidOperationException("The requested database is not owned by this harness run.");
        }

        var mismatches = new List<string>();
        var unexpectedKeys = requested.Keys.Cast<string>()
            .Where(key => !allowedKeys.Contains(key))
            .ToArray();
        if (unexpectedKeys.Length > 0) mismatches.Add($"connection keys ({string.Join(", ", unexpectedKeys)})");
        if (IsProtectedDatabase(expectedDatabase)) mismatches.Add("protected database");
        if (requested.Host != "127.0.0.1") mismatches.Add("host");
        if (requested.Port != owner.Port || requested.Port != expectedPort) mismatches.Add("port");
        if (requested.Database != expectedDatabase) mismatches.Add("database");
        if (requested.Username != expectedUser) mismatches.Add("username");
        // Npgsql intentionally omits the password from ConnectionString after a successful open
        // unless Persist Security Info is enabled. Closed connections must still carry this run's
        // harness-generated password; an open connection has already authenticated to the server.
        if (connection.State != System.Data.ConnectionState.Open &&
            requested.Password != PasswordFor(owner, expectedUser))
            mismatches.Add("password");
        if (requested.Pooling) mismatches.Add("pooling");
        if (requested.SslMode != SslMode.Disable) mismatches.Add("SSL mode");
        if (requested.ApplicationName != ApplicationNameFor(owner)) mismatches.Add("harness execution identity");
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL connection is not the active harness-owned identity; mismatched fields: " +
                string.Join(", ", mismatches) + ". Credential values are intentionally omitted.");
        }
    }

    private static NpgsqlConnectionStringBuilder CreateConnectionString(
        OwnedCluster owner,
        string database,
        string user) => new()
    {
        Host = "127.0.0.1",
        Port = owner.Port,
        Database = database,
        Username = user,
        Password = PasswordFor(owner, user),
        Pooling = false,
        SslMode = SslMode.Disable,
        Timeout = 5,
        CommandTimeout = 30,
        ApplicationName = ApplicationNameFor(owner)
    };

    private static string PasswordFor(OwnedCluster owner, string user) =>
        user == StandardUser ? owner.SuperuserPassword :
        user == CancellationUser ? owner.CancellationPassword :
        throw new InvalidOperationException("The PostgreSQL test user is not approved.");

    private static string ApplicationNameFor(OwnedCluster owner) =>
        $"{HarnessApplicationName}:{owner.RunId}";

    private static OwnedCluster RequireOwnedCluster() =>
        _owned ?? throw new InvalidOperationException(
            "No PostgreSQL cluster was started and registered by this test harness; no connection was attempted.");

    private static void EnsureOwnedCluster(OwnedCluster owner)
    {
        var fullRoot = Path.GetFullPath(owner.ClusterRoot);
        if (!IsWithinPath(fullRoot, owner.TempRoot) || IsProtectedDataDirectory(fullRoot) ||
            HasReparsePointInPath(fullRoot) || !File.Exists(owner.MarkerPath) ||
            !string.Equals(File.ReadAllText(owner.MarkerPath), owner.RunId, StringComparison.Ordinal) ||
            ReadAndValidatePostmasterPid(owner) != owner.PostgresProcessId)
        {
            throw new InvalidOperationException("The harness-owned PostgreSQL cluster identity is no longer valid.");
        }

        using var process = Process.GetProcessById(owner.PostgresProcessId);
        if (process.HasExited ||
            !string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty),
                Path.GetFullPath(owner.PostgresExecutable), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The PostgreSQL process is not the process started for this harness run.");
        }
    }

    private static int ReadAndValidatePostmasterPid(OwnedCluster owner)
    {
        var pidPath = Path.Combine(owner.DataDirectory, "postmaster.pid");
        if (!File.Exists(pidPath))
        {
            throw new InvalidOperationException("The harness-owned PostgreSQL process identity file is missing.");
        }

        var lines = File.ReadAllLines(pidPath);
        if (lines.Length < 4 || !int.TryParse(lines[0], out var processId) ||
            !string.Equals(Path.GetFullPath(lines[1]).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(owner.DataDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(lines[3], out var port) || port != owner.Port)
        {
            throw new InvalidOperationException("The harness-owned PostgreSQL process identity file is invalid.");
        }

        return processId;
    }

    private static string FindPostgresBinDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("GYMFLOW_POSTGRES_BIN");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = Path.GetFullPath(configured);
            if (IsProtectedDataDirectory(full))
            {
                throw new InvalidOperationException("PostgreSQL executables cannot be resolved from the protected recovery volume.");
            }
            return full;
        }

        var executable = OperatingSystem.IsWindows() ? "initdb.exe" : "initdb";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), executable);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(directory.Trim('"'));
            }
        }

        throw new InvalidOperationException(
            "Set GYMFLOW_POSTGRES_BIN to the PostgreSQL 17 bin directory or add PostgreSQL 17 to PATH.");
    }

    private static int ReservePort()
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (!IsReservedPort(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("A free, unreserved loopback PostgreSQL test port could not be selected.");
    }

    private static void EnsureDatabaseAbsent(NpgsqlConnection connection, string databaseName)
    {
        using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", databaseName);
        if ((bool)command.ExecuteScalar()!)
        {
            throw new InvalidOperationException("A base test database unexpectedly exists in the new cluster.");
        }
    }

    private static void EnsureDatabaseExists(NpgsqlConnection connection, string databaseName)
    {
        using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", databaseName);
        if (!(bool)command.ExecuteScalar()!)
        {
            throw new InvalidOperationException("A harness base test database was not initialized.");
        }
    }

    private static void ExecuteDdl(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void WritePrivateFile(string path, string value)
    {
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.WriteLine(value);
        }

        if (OperatingSystem.IsWindows())
        {
            var user = WindowsIdentity.GetCurrent().User ??
                throw new InvalidOperationException("The current Windows identity could not be resolved.");
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            System.IO.FileSystemAclExtensions.SetAccessControl(new FileInfo(path), security);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void ApplyPrivateDirectoryPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var user = WindowsIdentity.GetCurrent().User ??
                throw new InvalidOperationException("The current Windows identity could not be resolved.");
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(
                user, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            System.IO.FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(path), security);
        }
        else
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static bool HasReparsePointInPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? throw new InvalidOperationException("The temporary path has no root.");
        var current = root;
        foreach (var segment in full[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinPath(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void RunTool(
        string executable,
        IReadOnlyCollection<string> arguments,
        string workingDirectory,
        bool redirectOutput = true,
        string? diagnosticLogPath = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("A PostgreSQL test utility could not be started.");
        }

        Task<string>? stdout = redirectOutput ? process.StandardOutput.ReadToEndAsync() : null;
        Task<string>? stderr = redirectOutput ? process.StandardError.ReadToEndAsync() : null;
        if (!process.WaitForExit(ToolProcessTimeoutMilliseconds))
        {
            try { process.Kill(); } catch { }
            var terminated = process.WaitForExit(ToolTerminationWaitMilliseconds);
            var streams = redirectOutput
                ? DrainToolOutput(process, stdout!, stderr!)
                : (string.Empty, string.Empty, true, (string?)null);
            var terminationStatus = terminated
                ? "The launched utility process was terminated."
                : $"The launched utility process PID {process.Id} did not exit after termination was requested.";
            throw new InvalidOperationException(
                $"PostgreSQL test utility {Path.GetFileName(executable)} timed out after " +
                $"{ToolProcessTimeoutMilliseconds} ms. {terminationStatus} " +
                BuildDiagnosticText(streams.Item1, streams.Item2, streams.Item4, diagnosticLogPath));
        }

        var output = redirectOutput
            ? DrainToolOutput(process, stdout!, stderr!)
            : (string.Empty, string.Empty, true, (string?)null);
        if (!output.Item3)
        {
            throw new InvalidOperationException(
                $"PostgreSQL test utility {Path.GetFileName(executable)} exited, but its output streams " +
                $"did not drain within {ToolStreamDrainTimeoutMilliseconds} ms. " +
                BuildDiagnosticText(output.Item1, output.Item2, output.Item4, diagnosticLogPath));
        }

        if (!string.IsNullOrWhiteSpace(output.Item4))
        {
            throw new InvalidOperationException(
                $"PostgreSQL test utility {Path.GetFileName(executable)} output capture failed. " +
                BuildDiagnosticText(output.Item1, output.Item2, output.Item4, diagnosticLogPath));
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PostgreSQL test utility {Path.GetFileName(executable)} failed with exit code " +
                $"{process.ExitCode}. " +
                BuildDiagnosticText(output.Item1, output.Item2, output.Item4, diagnosticLogPath));
        }
    }

    private static (string Stdout, string Stderr, bool Drained, string? ReadError) DrainToolOutput(
        Process process,
        Task<string> stdout,
        Task<string> stderr)
    {
        bool drained;
        string? readError = null;
        try
        {
            drained = Task.WhenAll(stdout, stderr).Wait(ToolStreamDrainTimeoutMilliseconds);
        }
        catch (AggregateException exception)
        {
            drained = true;
            readError = exception.Flatten().GetBaseException().Message;
        }

        if (!drained)
        {
            try { process.StandardOutput.Close(); } catch { }
            try { process.StandardError.Close(); } catch { }
            ObserveTaskFailure(stdout);
            ObserveTaskFailure(stderr);
        }

        return (
            GetCompletedOutput(stdout),
            GetCompletedOutput(stderr),
            drained,
            readError);
    }

    private static string GetCompletedOutput(Task<string> output) =>
        output.Status == TaskStatus.RanToCompletion ? output.Result : string.Empty;

    private static void ObserveTaskFailure(Task task)
    {
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static string BuildDiagnosticText(
        string standardOutput,
        string standardError,
        string? readError,
        string? diagnosticLogPath)
    {
        var sections = new List<string>();
        var stdoutTail = TrimDiagnostic(standardOutput);
        var stderrTail = TrimDiagnostic(standardError);
        if (!string.IsNullOrWhiteSpace(stdoutTail)) sections.Add($"stdout: {stdoutTail}");
        if (!string.IsNullOrWhiteSpace(stderrTail)) sections.Add($"stderr: {stderrTail}");
        if (!string.IsNullOrWhiteSpace(readError)) sections.Add($"stream read error: {TrimDiagnostic(readError)}");

        if (!string.IsNullOrWhiteSpace(diagnosticLogPath))
        {
            try
            {
                if (File.Exists(diagnosticLogPath))
                {
                    var logTail = TrimDiagnostic(string.Join(Environment.NewLine,
                        File.ReadLines(diagnosticLogPath).TakeLast(30)));
                    if (!string.IsNullOrWhiteSpace(logTail)) sections.Add($"server log tail: {logTail}");
                }
            }
            catch (IOException)
            {
                sections.Add("server log tail: unavailable");
            }
            catch (UnauthorizedAccessException)
            {
                sections.Add("server log tail: unavailable");
            }
        }

        return sections.Count == 0
            ? "No captured tool diagnostics were available."
            : string.Join(" | ", sections);
    }

    private static string TrimDiagnostic(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= MaximumToolDiagnosticCharacters
            ? trimmed
            : trimmed[^MaximumToolDiagnosticCharacters..];
    }

    private static void StopAndRemoveOwnedCluster(OwnedCluster owner)
    {
        try
        {
            if (File.Exists(owner.MarkerPath) &&
                string.Equals(File.ReadAllText(owner.MarkerPath), owner.RunId, StringComparison.Ordinal) &&
                Directory.Exists(owner.DataDirectory))
            {
                try
                {
                    RunTool(owner.PgCtlExecutable,
                        ["stop", "--pgdata", owner.DataDirectory, "--mode", "fast", "--wait", "--timeout", "15"],
                        owner.ClusterRoot, redirectOutput: false,
                        diagnosticLogPath: Path.Combine(owner.ClusterRoot, "postgres.log"));
                }
                catch
                {
                    // A failed start has no server to stop; cleanup is still confined to the run marker.
                }

                if (IsWithinPath(Path.GetFullPath(owner.ClusterRoot), owner.TempRoot) &&
                    !IsProtectedDataDirectory(owner.ClusterRoot) &&
                    !HasReparsePointInPath(owner.ClusterRoot) &&
                    File.Exists(owner.MarkerPath) &&
                    string.Equals(File.ReadAllText(owner.MarkerPath), owner.RunId, StringComparison.Ordinal))
                {
                    Directory.Delete(owner.ClusterRoot, recursive: true);
                }
            }
        }
        catch
        {
            // Never broaden cleanup beyond the exact private run directory.
        }
    }

    private static void OnProcessExit(object? sender, EventArgs args)
    {
        lock (Sync)
        {
            if (_owned is not null)
            {
                var owner = _owned;
                _owned = null;
                StopAndRemoveOwnedCluster(owner);
            }
        }
    }

    private static void ValidateOwnedServerStartup(OwnedCluster owner)
    {
        var marker = File.ReadAllText(owner.MarkerPath);
        if (!string.Equals(marker, owner.RunId, StringComparison.Ordinal) ||
            !IsWithinPath(owner.ClusterRoot, owner.TempRoot) ||
            IsProtectedDataDirectory(owner.ClusterRoot) ||
            HasReparsePointInPath(owner.ClusterRoot))
        {
            throw new InvalidOperationException("The PostgreSQL test cluster was not created in its private temporary directory.");
        }
    }

    private sealed class OwnedCluster(
        string runId,
        string tempRoot,
        string clusterRoot,
        string dataDirectory,
        string markerPath,
        string initDbExecutable,
        string pgCtlExecutable,
        string postgresExecutable,
        int port,
        string superuserPassword,
        string cancellationPassword)
    {
        public string RunId { get; } = runId;
        public string TempRoot { get; } = tempRoot;
        public string ClusterRoot { get; } = clusterRoot;
        public string DataDirectory { get; } = dataDirectory;
        public string MarkerPath { get; } = markerPath;
        public string InitDbExecutable { get; } = initDbExecutable;
        public string PgCtlExecutable { get; } = pgCtlExecutable;
        public string PostgresExecutable { get; } = postgresExecutable;
        public int Port { get; } = port;
        public string SuperuserPassword { get; } = superuserPassword;
        public string CancellationPassword { get; } = cancellationPassword;
        public int PostgresProcessId { get; set; }
        public System.Collections.Concurrent.ConcurrentDictionary<string, byte> CreatedDatabases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public System.Collections.Concurrent.ConcurrentDictionary<string, byte> ReservedDatabases { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
