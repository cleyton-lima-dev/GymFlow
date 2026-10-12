using GymFlow.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GymFlow.IntegrationTests.Dashboard;

// Opt-in only to a freshly initialized local cluster. Never consumes application connection strings.
internal sealed class DashboardTestDatabase(AppDbContext context, SqliteConnection? sqlite) : IAsyncDisposable
{
    public AppDbContext Context { get; } = context;

    public static async Task<DashboardTestDatabase> CreateAsync(bool postgres, DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        SqliteConnection? sqlite = null;
        if (postgres)
        {
            var directory = Environment.GetEnvironmentVariable("GYMFLOW_DASHBOARD_PG_DATA")
                ?? throw new InvalidOperationException("Disposable PostgreSQL cluster not configured.");
            var fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            var marker = Path.Combine(fullPath, "gymflow-dashboard-disposable.marker");
            if (!Path.GetFileName(fullPath).StartsWith("gymflow-dashboard-pg-", StringComparison.Ordinal) ||
                !File.Exists(marker) || File.ReadAllText(marker).Trim() != "GymFlow dashboard disposable cluster")
                throw new InvalidOperationException("Unverified PostgreSQL cluster: connection refused.");
            var pid = File.ReadAllLines(Path.Combine(fullPath, "postmaster.pid"));
            if (pid.Length < 8 || Path.GetFullPath(pid[1]).TrimEnd(Path.DirectorySeparatorChar) != fullPath ||
                pid[5].Trim() != "127.0.0.1" || pid[7].Trim() != "ready" ||
                !int.TryParse(pid[3], out var port) || port < 1024)
                throw new InvalidOperationException("Cluster destination is not verified loopback.");

            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1", Port = port, Username = "gymflow_dashboard_test",
                Database = "postgres", Pooling = false, Timeout = 5
            };
            await using var admin = new NpgsqlConnection(connection.ConnectionString);
            await admin.OpenAsync();
            await using var check = new NpgsqlCommand("SELECT current_setting('data_directory')", admin);
            var actual = (string)(await check.ExecuteScalarAsync())!;
            if (Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar) != fullPath)
                throw new InvalidOperationException("Server does not belong to the disposable test cluster.");
            // Unique databases are retained inside this disposable cluster; no existing database is dropped.
            var database = "gymflow_dashboard_" + Guid.NewGuid().ToString("N");
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
            await create.ExecuteNonQueryAsync();
            connection.Database = database;
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
        return new DashboardTestDatabase(db, sqlite);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        if (sqlite != null) await sqlite.DisposeAsync();
    }
}

internal sealed class DashboardPostgresFactAttribute : FactAttribute
{
    public DashboardPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GYMFLOW_DASHBOARD_PG_DATA")))
            Skip = "Requires an explicitly verified disposable loopback PostgreSQL cluster.";
    }
}

internal sealed class DashboardPostgresTheoryAttribute : TheoryAttribute
{
    public DashboardPostgresTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GYMFLOW_DASHBOARD_PG_DATA")))
            Skip = "Requires an explicitly verified disposable loopback PostgreSQL cluster.";
    }
}
