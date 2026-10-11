using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Persistence;

public class EfApplicationTransactionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ShouldCommitChanges()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database
            .ExecuteSqlRawAsync(
                """
                CREATE TABLE AtomicityProbe
                (
                    Id INTEGER PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                """);

        var transaction =
            new EfApplicationTransaction(
                context);

        await transaction
            .ExecuteAsync(
                async () =>
                {
                    await context.Database
                        .ExecuteSqlRawAsync(
                            """
                            INSERT INTO AtomicityProbe
                                (Id, Value)
                            VALUES
                                (1, 'committed');
                            """);
                });

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "SELECT COUNT(*) FROM AtomicityProbe;";

        var count =
            Convert.ToInt32(
                await command
                    .ExecuteScalarAsync());

        Assert.Equal(
            1,
            count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_ShouldRollbackChanges()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database
            .ExecuteSqlRawAsync(
                """
                CREATE TABLE AtomicityProbe
                (
                    Id INTEGER PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                """);

        var transaction =
            new EfApplicationTransaction(
                context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                transaction.ExecuteAsync(
                    async () =>
                    {
                        await context.Database
                            .ExecuteSqlRawAsync(
                                """
                                INSERT INTO AtomicityProbe
                                    (Id, Value)
                                VALUES
                                    (1, 'must-rollback');
                                """);

                        throw new InvalidOperationException(
                            "Falha simulada.");
                    }));

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "SELECT COUNT(*) FROM AtomicityProbe;";

        var count =
            Convert.ToInt32(
                await command
                    .ExecuteScalarAsync());

        Assert.Equal(
            0,
            count);
    }
}