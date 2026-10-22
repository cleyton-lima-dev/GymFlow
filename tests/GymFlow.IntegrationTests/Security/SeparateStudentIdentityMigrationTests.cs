using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GymFlow.IntegrationTests.Security;

public sealed class SeparateStudentIdentityMigrationTests
{
    private const string PreviousMigration = "20261012225126_AddPlatformOnboarding";
    private const string TargetMigration = "20261013233723_SeparateStudentIdentity";

    [TenantEnrollmentAccessTests.PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_BackfillPreservesLegacyAuthenticationAndBlocks(bool postgres)
    {
        Assert.True(postgres);
        var adminConnection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("GYMFLOW_ISOLATED_POSTGRES"));
        Assert.Equal("127.0.0.1", adminConnection.Host);
        Assert.Equal(55439, adminConnection.Port);
        Assert.Equal("gymflow_codex_test_base", adminConnection.Database);
        Assert.Equal("gymflow_test", adminConnection.Username);

        var expectedDataDirectory = Environment.GetEnvironmentVariable(
            "GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(expectedDataDirectory));
        Assert.True(File.Exists(Path.Combine(expectedDataDirectory!, "PG_VERSION")));

        await using var admin = new NpgsqlConnection(adminConnection.ConnectionString);
        await admin.OpenAsync();
        await using (var verify = new NpgsqlCommand(
            "SELECT current_setting('data_directory')", admin))
        {
            var actual = (string)(await verify.ExecuteScalarAsync())!;
            Assert.Equal(Path.GetFullPath(expectedDataDirectory!).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar),
                StringComparer.OrdinalIgnoreCase);
        }

        var database = $"gymflow_codex_test_{Guid.NewGuid():N}";
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync();

        var testConnection = new NpgsqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Database = database,
            Pooling = false
        };
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(testConnection.ConnectionString)
                .Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
            var cases = new[]
            {
                new LegacyState("active", true, null, null),
                new LegacyState("manual", false, (int)StudentInactivationReason.Manual, null),
                new LegacyState("automatic", false, (int)StudentInactivationReason.NoValidEnrollment, null),
                new LegacyState("archived", true, null, now.AddDays(-10))
            };

            await using (var connection = new NpgsqlConnection(testConnection.ConnectionString))
            {
                await connection.OpenAsync();
                foreach (var state in cases)
                {
                    var userId = Guid.NewGuid();
                    var studentId = Guid.NewGuid();
                    state.UserId = userId;
                    state.StudentId = studentId;
                    await using (var insertUser = new NpgsqlCommand("""
                        INSERT INTO "Users" ("Id", "GymId", "Name", "Email", "PasswordHash", "Role", "IsActive", "CreatedAt")
                        VALUES (@id, @gymId, @name, @email, 'synthetic-hash', @role, @active, @createdAt)
                        """, connection))
                    {
                        insertUser.Parameters.AddWithValue("id", userId);
                        insertUser.Parameters.AddWithValue("gymId", Guid.NewGuid());
                        insertUser.Parameters.AddWithValue("name", $"Legacy {state.Name}");
                        insertUser.Parameters.AddWithValue("email", $"{state.Name}-{userId:N}@example.test");
                        insertUser.Parameters.AddWithValue("role", (int)UserRole.Student);
                        insertUser.Parameters.AddWithValue("active", state.UserIsActive);
                        insertUser.Parameters.AddWithValue("createdAt", now);
                        await insertUser.ExecuteNonQueryAsync();
                    }

                    await using var insertStudent = new NpgsqlCommand("""
                        INSERT INTO "Students" ("Id", "UserId", "CreatedAt", "InactivationReason", "InactivatedAt", "ArchivedAt")
                        VALUES (@id, @userId, @createdAt, @reason, @inactivatedAt, @archivedAt)
                        """, connection);
                    insertStudent.Parameters.AddWithValue("id", studentId);
                    insertStudent.Parameters.AddWithValue("userId", userId);
                    insertStudent.Parameters.AddWithValue("createdAt", now);
                    insertStudent.Parameters.AddWithValue("reason", (object?)state.InactivationReason ?? DBNull.Value);
                    insertStudent.Parameters.AddWithValue("inactivatedAt",
                        state.InactivationReason is null ? DBNull.Value : now.AddDays(-20));
                    insertStudent.Parameters.AddWithValue("archivedAt", (object?)state.ArchivedAt ?? DBNull.Value);
                    await insertStudent.ExecuteNonQueryAsync();
                }
            }

            await migrator.MigrateAsync(TargetMigration);

            await using (var verification = new NpgsqlConnection(testConnection.ConnectionString))
            {
                await verification.OpenAsync();
                await using var verifyRows = new NpgsqlCommand("""
                    SELECT s."Id", s."UserId", u."IsActive", s."InactivationReason", s."ArchivedAt",
                           s."IsActive", s."AppAccessEnabled"
                    FROM "Students" s JOIN "Users" u ON u."Id" = s."UserId"
                    """, verification);
                await using var reader = await verifyRows.ExecuteReaderAsync();
                var expectedByStudent = cases.ToDictionary(x => x.StudentId);
                var seen = new HashSet<Guid>();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(0);
                    Assert.True(expectedByStudent.TryGetValue(id, out var state));
                    Assert.True(seen.Add(id));
                    Assert.Equal(state!.UserId, reader.GetGuid(1));
                    Assert.Equal(state.UserIsActive, reader.GetBoolean(2));
                    Assert.Equal(state.InactivationReason, reader.IsDBNull(3) ? null : reader.GetInt32(3));
                    Assert.Equal(state.ArchivedAt, reader.IsDBNull(4) ? null : reader.GetDateTime(4));
                    var expectedStudentActive = state.InactivationReason switch
                    {
                        (int)StudentInactivationReason.Manual => false,
                        (int)StudentInactivationReason.NoValidEnrollment => true,
                        _ => state.UserIsActive
                    };
                    Assert.Equal(expectedStudentActive, reader.GetBoolean(5));
                    Assert.Equal(state.UserIsActive &&
                                 state.InactivationReason != (int)StudentInactivationReason.Manual &&
                                 state.ArchivedAt is null,
                        reader.GetBoolean(6));
                }
                Assert.Equal(cases.Length, seen.Count);
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class LegacyState(string name, bool userIsActive, int? inactivationReason, DateTime? archivedAt)
    {
        public string Name { get; } = name;
        public bool UserIsActive { get; } = userIsActive;
        public int? InactivationReason { get; } = inactivationReason;
        public DateTime? ArchivedAt { get; } = archivedAt;
        public Guid UserId { get; set; }
        public Guid StudentId { get; set; }
    }
}
