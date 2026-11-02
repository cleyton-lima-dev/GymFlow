using GymFlow.Infrastructure.Data;
using GymFlow.IntegrationTests.Infrastructure;
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
            var connection = DisposablePostgresCluster.BuildConnectionString(
                "GYMFLOW_ISOLATED_POSTGRES", "GYMFLOW_ISOLATED_POSTGRES_PORT",
                "gymflow_codex_test_base", "gymflow_test", "GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY");
            postgresAdmin = connection.ConnectionString;
            await using var admin = new NpgsqlConnection(postgresAdmin);
            await DisposablePostgresCluster.OpenVerifiedConnectionAsync(admin);
            postgresDatabase = DisposablePostgresCluster.NewDatabaseName("gymflow_codex_test_dashboard_");
            await DisposablePostgresCluster.CreateDatabaseAsync(admin, postgresDatabase);
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
        if (postgres)
        {
            await DisposablePostgresCluster.RunWithVerifiedConnectionAsync(
                (NpgsqlConnection)db.Database.GetDbConnection(),
                () => db.Database.EnsureCreatedAsync()); // Brand-new harness-owned test DB only; no migrations.
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
        return new DashboardTestDatabase(db, sqlite, postgresAdmin, postgresDatabase);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        if (sqlite != null) await sqlite.DisposeAsync();
        if (PostgresDatabase is not null)
        {
            await using var admin = new NpgsqlConnection(PostgresAdmin);
            await DisposablePostgresCluster.OpenVerifiedConnectionAsync(admin);
            await DisposablePostgresCluster.DropDatabaseAsync(admin, PostgresDatabase);
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
