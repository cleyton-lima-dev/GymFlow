using GymFlow.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GymFlow.IntegrationTests.Dashboard;

// Opt-in only to a freshly initialized local cluster. Never consumes application connection strings.
internal sealed class DashboardTestDatabase(AppDbContext context, SqliteConnection? sqlite,
    string? postgresAdmin, string? postgresDatabase) : IAsyncDisposable
{
    public AppDbContext Context { get; } = context;
    private string? PostgresAdmin { get; } = postgresAdmin;
    private string? PostgresDatabase { get; } = postgresDatabase;

    public static async Task<DashboardTestDatabase> CreateAsync(bool postgres, DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        SqliteConnection? sqlite = null;
        string? postgresAdmin = null;
        string? postgresDatabase = null;
        if (postgres)
        {
            var connection = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")
                ?? throw new InvalidOperationException("The verified isolated PostgreSQL connection is not configured."));
            if (connection.Host != "127.0.0.1" || connection.Port != 55439 ||
                connection.Database != "gymflow_codex_test_base" || connection.Username != "gymflow_test")
                throw new InvalidOperationException("Only the explicitly isolated local PostgreSQL cluster is allowed.");
            connection.Pooling = false;
            var expectedDataDirectory = Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY");
            if (string.IsNullOrWhiteSpace(expectedDataDirectory) || !Path.IsPathFullyQualified(expectedDataDirectory) ||
                !File.Exists(Path.Combine(expectedDataDirectory, "PG_VERSION")))
                throw new InvalidOperationException("The verified disposable PostgreSQL data directory is required.");

            postgresAdmin = connection.ConnectionString;
            await using var admin = new NpgsqlConnection(postgresAdmin);
            await admin.OpenAsync();
            await using var check = new NpgsqlCommand("SELECT current_setting('data_directory')", admin);
            var actual = (string)(await check.ExecuteScalarAsync())!;
            if (!string.Equals(Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(expectedDataDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PostgreSQL is not the verified disposable cluster. No test database was created.");

            postgresDatabase = $"gymflow_codex_test_dashboard_{Guid.NewGuid():N}";
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{postgresDatabase}\"", admin);
            await create.ExecuteNonQueryAsync();
            connection.Database = postgresDatabase;
            options.UseNpgsql(connection.ConnectionString);
        }
        else
        {
            sqlite = new SqliteConnection("Data Source=:memory:");
            await sqlite.OpenAsync();
            options.UseSqlite(sqlite);
        }
        if (interceptor != null) options.AddInterceptors(interceptor);
        var db = new AppDbContext(options.Options);
        await db.Database.EnsureCreatedAsync(); // Brand-new test DB only; no migrations.
        return new DashboardTestDatabase(db, sqlite, postgresAdmin, postgresDatabase);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        if (sqlite != null) await sqlite.DisposeAsync();
        if (PostgresDatabase is not null)
        {
            await using var admin = new NpgsqlConnection(PostgresAdmin);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{PostgresDatabase}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

internal sealed class DashboardPostgresFactAttribute : FactAttribute
{
    public DashboardPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY")))
            Skip = "Requires an explicitly verified disposable loopback PostgreSQL cluster.";
    }
}

internal sealed class DashboardPostgresTheoryAttribute : TheoryAttribute
{
    public DashboardPostgresTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY")))
            Skip = "Requires an explicitly verified disposable loopback PostgreSQL cluster.";
    }
}
