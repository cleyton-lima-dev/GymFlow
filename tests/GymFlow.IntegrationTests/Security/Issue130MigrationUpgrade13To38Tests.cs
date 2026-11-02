using System.Diagnostics;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Security;
using GymFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Abstractions;

namespace GymFlow.IntegrationTests.Security;

public sealed class Issue130MigrationUpgrade13To38Tests(ITestOutputHelper output)
{
    private const string Initial13 = "20260914192425_AddWorkoutTemplateDayNotes";
    private const string StandardDatabase = "gymflow_codex_test_base";
    private const string StandardUser = "gymflow_test";

    [Fact]
    public async Task SyntheticLegacySchema13UpgradesTo38AndPreservesReferentialIntegrity()
    {
        var phase = "harness";
        var total = Stopwatch.StartNew();
        var migrations13Time = TimeSpan.Zero;
        var migration25Time = TimeSpan.Zero;
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var professorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var templateDayId = Guid.NewGuid();
        var templateExerciseId = Guid.NewGuid();
        var workoutId = Guid.NewGuid();
        var workoutDayId = Guid.NewGuid();
        var workoutExerciseId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var studentEmail = "student-" + Guid.NewGuid().ToString("N") + "@example.test";
        const string password = "synthetic-only-password-130";

        var baseConnection = DisposablePostgresCluster.BuildConnectionString(
            "GYMFLOW_ISOLATED_POSTGRES",
            "GYMFLOW_ISOLATED_POSTGRES_PORT",
            StandardDatabase,
            StandardUser,
            "GYMFLOW_ISOLATED_POSTGRES_DATA_DIRECTORY");
        Assert.StartsWith("GymFlow.DisposableTestHarness:", baseConnection.ApplicationName);
        output.WriteLine("HARNESS_APPLICATION_NAME=PRESERVED");
        await using var admin = new NpgsqlConnection(
            DisposablePostgresCluster.CreateVerifiedAdministrativeConnectionString());
        await DisposablePostgresCluster.OpenVerifiedConnectionAsync(admin);

        var database = DisposablePostgresCluster.NewDatabaseName("gymflow_codex_test_");
        await DisposablePostgresCluster.CreateDatabaseAsync(admin, database);
        var testConnection = new NpgsqlConnectionStringBuilder(baseConnection.ConnectionString)
        {
            Database = database,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 15
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(testConnection.ConnectionString)
            .Options;

        try
        {
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            var ownedConnection = (NpgsqlConnection)db.Database.GetDbConnection();
            var expectedMigrations = db.Database.GetMigrations().ToArray();
            Assert.Equal(38, expectedMigrations.Length);
            Assert.Equal(Initial13, expectedMigrations[12]);
            output.WriteLine("EXPECTED_MIGRATIONS=38; baseline_last=" + Initial13);

            phase = "migrations-1-through-13";
            var migration13Timer = Stopwatch.StartNew();
            await DisposablePostgresCluster.RunWithVerifiedConnectionAsync(
                ownedConnection,
                () => migrator.MigrateAsync(Initial13));
            migration13Timer.Stop();
            migrations13Time = migration13Timer.Elapsed;
            var appliedBefore = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(expectedMigrations.Take(13), appliedBefore);
            Assert.Equal(13, appliedBefore.Length);
            output.WriteLine("EF_BEFORE=13; unknown=0; pending=25");

            phase = "synthetic-seed";
            await using (var connection = new NpgsqlConnection(testConnection.ConnectionString))
            {
                await DisposablePostgresCluster.OpenVerifiedConnectionAsync(connection);
                var hasher = new PasswordHasher();
                await InsertUserAsync(connection, adminId, gymId, "Synthetic Admin", "admin-" + Guid.NewGuid().ToString("N") + "@example.test", UserRole.Admin, hasher.Hash(password), now);
                await InsertUserAsync(connection, professorId, gymId, "Synthetic Professor", "professor-" + Guid.NewGuid().ToString("N") + "@example.test", UserRole.Professor, hasher.Hash(password), now);
                await InsertUserAsync(connection, userId, gymId, "Synthetic Student", studentEmail, UserRole.Student, hasher.Hash(password), now);
                await InsertStudentAsync(connection, studentId, userId, now);

                await ExecuteAsync(connection, """
                    INSERT INTO "Exercises" ("Id", "GymId", "Name", "MuscleGroup", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                    VALUES (@id, @gym, 'Synthetic Squat', 'Legs', 'Synthetic only', TRUE, @now, @now)
                    """,
                    ("id", exerciseId), ("gym", gymId), ("now", now));

                await ExecuteAsync(connection, """
                    INSERT INTO "WorkoutTemplates" ("Id", "GymId", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                    VALUES (@id, @gym, 'Synthetic Plan', 'Synthetic only', TRUE, @now, @now)
                    """,
                    ("id", templateId), ("gym", gymId), ("now", now));
                await ExecuteAsync(connection, """
                    INSERT INTO "WorkoutTemplateDays" ("Id", "WorkoutTemplateId", "Name", "Order")
                    VALUES (@id, @template, 'Synthetic Day', 1)
                    """,
                    ("id", templateDayId), ("template", templateId));
                await ExecuteAsync(connection, """
                    INSERT INTO "WorkoutTemplateExercises" ("Id", "WorkoutTemplateDayId", "ExerciseId", "Sets", "Repetitions", "RestSeconds", "Notes", "Order")
                    VALUES (@id, @day, @exercise, 3, '8', 60, 'Synthetic only', 1)
                    """,
                    ("id", templateExerciseId), ("day", templateDayId), ("exercise", exerciseId));

                await ExecuteAsync(connection, """
                    INSERT INTO "Workouts" ("Id", "StudentId", "GymId", "SourceWorkoutTemplateId", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                    VALUES (@id, @student, @gym, @template, 'Synthetic Workout', 'Synthetic only', TRUE, @now, @now)
                    """,
                    ("id", workoutId), ("student", studentId), ("gym", gymId), ("template", templateId), ("now", now));
                await ExecuteAsync(connection, """
                    INSERT INTO "WorkoutDays" ("Id", "WorkoutId", "Name", "Order")
                    VALUES (@id, @workout, 'Synthetic Day', 1)
                    """,
                    ("id", workoutDayId), ("workout", workoutId));
                await ExecuteAsync(connection, """
                    INSERT INTO "WorkoutExercises" ("Id", "WorkoutDayId", "ExerciseId", "Sets", "Repetitions", "RestSeconds", "Notes", "Order")
                    VALUES (@id, @day, @exercise, 3, '8', 60, 'Synthetic only', 1)
                    """,
                    ("id", workoutExerciseId), ("day", workoutDayId), ("exercise", exerciseId));
                await ExecuteAsync(connection, """
                    INSERT INTO "WorkoutExecutions" ("Id", "WorkoutDayId", "CompletedAt", "ExecutionDate")
                    VALUES (@id, @day, @now, @date)
                    """,
                    ("id", executionId), ("day", workoutDayId), ("now", now), ("date", DateOnly.FromDateTime(now)));
                await ExecuteAsync(connection, """
                    INSERT INTO "PhysicalAssessments" ("Id", "StudentId", "AssessmentDate", "WeightKg", "HeightCm", "Notes", "CreatedAt", "UpdatedAt")
                    VALUES (@id, @student, @date, 70.0, 170.0, 'Synthetic only', @now, @now)
                    """,
                    ("id", assessmentId), ("student", studentId), ("date", DateOnly.FromDateTime(now)), ("now", now));
            }

            phase = "verify-13-before-upgrade";
            await using (var connection = new NpgsqlConnection(testConnection.ConnectionString))
            {
                await DisposablePostgresCluster.OpenVerifiedConnectionAsync(connection);
                var expectedTables = await ScalarIntAsync(connection, """
                    SELECT count(*) FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name IN ('Users', 'Students', 'Exercises', 'WorkoutTemplates', 'Workouts', 'PhysicalAssessments')
                    """);
                Assert.Equal(6, expectedTables);
                Assert.Equal(1, await CountByIdAsync(connection, "Users", adminId));
                Assert.Equal(1, await CountByIdAsync(connection, "Users", professorId));
                Assert.Equal(1, await CountByIdAsync(connection, "Users", userId));
                Assert.Equal(1, await CountByIdAsync(connection, "Students", studentId));
                Assert.Equal(1, await CountByIdAsync(connection, "Exercises", exerciseId));
                Assert.Equal(1, await CountByIdAsync(connection, "WorkoutTemplates", templateId));
                Assert.Equal(1, await CountByIdAsync(connection, "Workouts", workoutId));
                Assert.Equal(1, await CountByIdAsync(connection, "WorkoutExecutions", executionId));
                Assert.Equal(1, await CountByIdAsync(connection, "PhysicalAssessments", assessmentId));
                Assert.Equal(13, (await db.Database.GetAppliedMigrationsAsync()).Count());
            }
            output.WriteLine("SYNTHETIC_SEED=PASS; users=3; roles=admin,professor,student; students=1; workouts=1; executions=1; assessments=1");

            phase = "migrations-14-through-38";
            var migration25Timer = Stopwatch.StartNew();
            var pendingBeforeUpgrade = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            Assert.Equal(25, pendingBeforeUpgrade.Length);
            Assert.Equal(expectedMigrations.Skip(13), pendingBeforeUpgrade);
            output.WriteLine("EF_PENDING=25; expected_sequence=verified");
            foreach (var migrationId in pendingBeforeUpgrade)
            {
                phase = "migration:" + migrationId;
                output.WriteLine("MIGRATION_START=" + migrationId);
                var oneMigration = Stopwatch.StartNew();
                await DisposablePostgresCluster.RunWithVerifiedConnectionAsync(
                    ownedConnection,
                    () => migrator.MigrateAsync(migrationId));
                oneMigration.Stop();
                output.WriteLine("MIGRATION_APPLIED=" + migrationId + "; elapsed_ms=" + oneMigration.ElapsedMilliseconds);
            }
            migration25Timer.Stop();
            migration25Time = migration25Timer.Elapsed;

            phase = "verify-final-schema-and-data";
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var known = expectedMigrations.ToHashSet(StringComparer.Ordinal);
            Assert.Empty(applied.Where(id => !known.Contains(id)));
            Assert.Equal(expectedMigrations, applied);
            Assert.Equal(38, applied.Length);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            output.WriteLine("EF_AFTER=38; unknown=0; pending=0");

            var unvalidatedForeignKeys = 0;
            var invalidIndexes = 0;
            var foreignKeyCount = 0;
            var validIndexCount = 0;
            await using (var connection = new NpgsqlConnection(testConnection.ConnectionString))
            {
                await DisposablePostgresCluster.OpenVerifiedConnectionAsync(connection);
                unvalidatedForeignKeys = await ScalarIntAsync(connection,
                    "SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND NOT convalidated");
                Assert.Equal(0, unvalidatedForeignKeys);
                invalidIndexes = await ScalarIntAsync(connection, "SELECT count(*) FROM pg_index WHERE NOT indisvalid");
                Assert.Equal(0, invalidIndexes);
                foreignKeyCount = await ScalarIntAsync(connection, "SELECT count(*) FROM pg_constraint WHERE contype = 'f'");
                validIndexCount = await ScalarIntAsync(connection, "SELECT count(*) FROM pg_index WHERE indisvalid");
                Assert.True(foreignKeyCount > 0);
                Assert.True(validIndexCount > 0);
                Assert.Equal(2, await ScalarIntAsync(connection, """
                    SELECT count(*) FROM pg_extension WHERE extname IN ('citext', 'unaccent')
                    """));
                Assert.Equal(1, await ScalarIntAsync(connection, """
                    SELECT count(*) FROM pg_indexes
                    WHERE schemaname = 'public'
                      AND indexname = 'IX_PhysicalAccessCredentials_OnePinPerStudent'
                    """));

                await using var verifyStudent = new NpgsqlCommand("""
                    SELECT s."GymId", s."Name", s."Email", s."IsActive", s."AppAccessEnabled",
                           s."UserId", u."GymId", u."Name", u."Email", u."Role", u."IsActive"
                    FROM "Students" s JOIN "Users" u ON u."Id" = s."UserId"
                    WHERE s."Id" = @student
                    """, connection);
                verifyStudent.Parameters.AddWithValue("student", studentId);
                await using (var reader = await verifyStudent.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(gymId, reader.GetGuid(0));
                    Assert.Equal("Synthetic Student", reader.GetString(1));
                    Assert.Equal(studentEmail, reader.GetString(2));
                    Assert.True(reader.GetBoolean(3));
                    Assert.True(reader.GetBoolean(4));
                    Assert.Equal(userId, reader.GetGuid(5));
                    Assert.Equal(gymId, reader.GetGuid(6));
                    Assert.Equal("Synthetic Student", reader.GetString(7));
                    Assert.Equal(studentEmail, reader.GetString(8));
                    Assert.Equal((int)UserRole.Student, reader.GetInt32(9));
                    Assert.True(reader.GetBoolean(10));
                    Assert.False(await reader.ReadAsync());
                }

                Assert.Equal(1, await CountByIdAsync(connection, "Users", userId));
                Assert.Equal(1, await CountByIdAsync(connection, "Users", professorId));
                Assert.Equal(1, await CountByIdAsync(connection, "Users", adminId));
                Assert.Equal(1, await CountByIdAsync(connection, "Students", studentId));
                Assert.Equal(1, await CountByIdAsync(connection, "Exercises", exerciseId));
                Assert.Equal(1, await CountByIdAsync(connection, "WorkoutTemplates", templateId));
                Assert.Equal(1, await CountByIdAsync(connection, "Workouts", workoutId));
                Assert.Equal(1, await CountByIdAsync(connection, "WorkoutExecutions", executionId));
                Assert.Equal(1, await CountByIdAsync(connection, "PhysicalAssessments", assessmentId));
                await using var verifyLinks = new NpgsqlCommand("""
                    SELECT
                        (SELECT count(*) FROM "Workouts" w
                         JOIN "Students" s ON s."Id" = w."StudentId"
                         JOIN "WorkoutTemplates" t ON t."Id" = w."SourceWorkoutTemplateId"
                         WHERE w."Id" = @workout AND w."StudentId" = @student AND w."GymId" = @gym) AS workout_links,
                        (SELECT count(*) FROM "WorkoutExecutions" e
                         JOIN "WorkoutDays" d ON d."Id" = e."WorkoutDayId"
                         JOIN "Workouts" w ON w."Id" = d."WorkoutId"
                         WHERE e."Id" = @execution AND w."StudentId" = @student AND w."GymId" = @gym) AS execution_links,
                        (SELECT count(*) FROM "PhysicalAssessments" a
                         JOIN "Students" s ON s."Id" = a."StudentId"
                         WHERE a."Id" = @assessment AND a."StudentId" = @student) AS assessment_links,
                        (SELECT count(*) FROM "WorkoutTemplateExercises" x
                         JOIN "WorkoutTemplateDays" d ON d."Id" = x."WorkoutTemplateDayId"
                         JOIN "WorkoutTemplates" t ON t."Id" = d."WorkoutTemplateId"
                         JOIN "Exercises" e ON e."Id" = x."ExerciseId"
                         WHERE x."Id" = @templateExercise AND t."Id" = @template AND e."Id" = @exercise) AS template_links
                    """, connection);
                verifyLinks.Parameters.AddWithValue("workout", workoutId);
                verifyLinks.Parameters.AddWithValue("student", studentId);
                verifyLinks.Parameters.AddWithValue("gym", gymId);
                verifyLinks.Parameters.AddWithValue("execution", executionId);
                verifyLinks.Parameters.AddWithValue("assessment", assessmentId);
                verifyLinks.Parameters.AddWithValue("templateExercise", templateExerciseId);
                verifyLinks.Parameters.AddWithValue("template", templateId);
                verifyLinks.Parameters.AddWithValue("exercise", exerciseId);
                await using var linksReader = await verifyLinks.ExecuteReaderAsync();
                Assert.True(await linksReader.ReadAsync());
                for (var column = 0; column < 4; column++) Assert.Equal(1L, linksReader.GetInt64(column));
                output.WriteLine("SYNTHETIC_LINKS=PASS; student_user=1; workout_student_gym_template=1; execution_student_gym=1; assessment_student=1; template_exercise=1");
            }

            output.WriteLine("INTEGRITY=PASS; synthetic_links=verified; foreign_keys=" + foreignKeyCount + "; invalid_foreign_keys=" + unvalidatedForeignKeys + "; valid_indexes=" + validIndexCount + "; invalid_indexes=" + invalidIndexes);
            output.WriteLine("UPGRADE13_38=PASS; applied=38; pending=0; unknown=0; citext_unaccent=2; synthetic_records=preserved");
            output.WriteLine("MIGRATION_TIMES_MS=initial13:" + migration13Timer.ElapsedMilliseconds + "; remaining25:" + migration25Timer.ElapsedMilliseconds);
            output.WriteLine("TOTAL_MS=" + total.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            var postgres = FindPostgresException(exception);
            output.WriteLine("UPGRADE13_38=FAIL; phase=" + phase + "; exception=" + exception.GetType().Name + "; SQLSTATE=" + (postgres?.SqlState ?? "none"));
            throw;
        }
        finally
        {
            await DisposablePostgresCluster.DropDatabaseAsync(admin, database);
        }
    }

    private static async Task InsertUserAsync(
        NpgsqlConnection connection,
        Guid id,
        Guid gymId,
        string name,
        string email,
        UserRole role,
        string passwordHash,
        DateTime createdAt)
    {
        await ExecuteAsync(connection, """
            INSERT INTO "Users" ("Id", "GymId", "Name", "Email", "PasswordHash", "Role", "IsActive", "CreatedAt")
            VALUES (@id, @gym, @name, @email, @hash, @role, TRUE, @createdAt)
            """,
            ("id", id), ("gym", gymId), ("name", name), ("email", email),
            ("hash", passwordHash), ("role", (int)role), ("createdAt", createdAt));
    }

    private static async Task InsertStudentAsync(NpgsqlConnection connection, Guid studentId, Guid userId, DateTime createdAt)
    {
        await ExecuteAsync(connection, """
            INSERT INTO "Students" ("Id", "UserId", "BirthDate", "Phone", "CreatedAt", "UpdatedAt")
            VALUES (@id, @user, DATE '2000-01-01', '5550100', @createdAt, @createdAt)
            """,
            ("id", studentId), ("user", userId), ("createdAt", createdAt));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarIntAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountByIdAsync(NpgsqlConnection connection, string table, Guid id)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Users", "Students", "Exercises", "WorkoutTemplates", "Workouts",
            "WorkoutExecutions", "PhysicalAssessments"
        };
        Assert.Contains(table, allowed);
        return await ScalarIntAsync(connection, "SELECT count(*) FROM \"" + table + "\" WHERE \"Id\" = '" + id.ToString("D") + "'::uuid");
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres)
                return postgres;
        return null;
    }

}
