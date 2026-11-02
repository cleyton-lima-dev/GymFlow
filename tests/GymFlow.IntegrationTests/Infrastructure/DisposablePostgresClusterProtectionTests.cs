using Npgsql;

namespace GymFlow.IntegrationTests.Infrastructure;

public sealed class DisposablePostgresClusterProtectionTests
{
    [Fact]
    public void RecoveryPortIsReserved()
    {
        Assert.True(DisposablePostgresCluster.IsReservedPort(49357));
    }

    [Theory]
    [InlineData("issue130_restore_avelri")]
    [InlineData("issue130_erp_work_20261101")]
    [InlineData("gymflow_dev")]
    [InlineData("gymflow_issue130_manual_20261026")]
    public void ProtectedDatabasesAreRejected(string database)
    {
        Assert.True(DisposablePostgresCluster.IsProtectedDatabase(database));
    }

    [Theory]
    [InlineData("R:\\")]
    [InlineData("R:\\Issue130\\RestorePostgres17\\data")]
    [InlineData("r:/Issue130/RestorePostgres17/data")]
    public void ProtectedRecoveryVolumeIsRejected(string dataDirectory)
    {
        Assert.True(DisposablePostgresCluster.IsProtectedDataDirectory(dataDirectory));
    }

    [Fact]
    public void ValidLocalOptInIsAcceptedWithoutStartingOrConnectingToAnyServer()
    {
        var tempDataPath = Path.Combine(Path.GetTempPath(), "not-created", "data");

        var exception = Record.Exception(() => DisposablePostgresCluster.ValidateOptInTarget(
            "Host=127.0.0.1;Port=55439;Database=gymflow_codex_test_base;Username=gymflow_test;Password=synthetic;SSL Mode=Disable",
            "55439",
            tempDataPath,
            "gymflow_codex_test_base",
            "gymflow_test"));

        Assert.Null(exception);
    }

    [Fact]
    public void RecoveryPortAndDataDirectoryOptInFailsBeforeClusterStartup()
    {
        var originalConnection = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION");
        var originalPort = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT");
        var originalDataDirectory = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA");
        try
        {
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION",
                "Host=127.0.0.1;Port=49357;Database=gymflow_codex_test_base;Username=gymflow_test;Password=synthetic;SSL Mode=Disable");
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT", "49357");
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA", @"R:\Issue130\RestorePostgres17\data");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                DisposablePostgresCluster.BuildConnectionString(
                    "GYMFLOW_TEST_GUARD_CONNECTION",
                    "GYMFLOW_TEST_GUARD_PORT",
                    "gymflow_codex_test_base",
                    "gymflow_test",
                    "GYMFLOW_TEST_GUARD_DATA"));

            Assert.Contains("prohibited", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION", originalConnection);
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT", originalPort);
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA", originalDataDirectory);
        }
    }

    [Fact]
    public void ExplicitProtectedPortDatabaseAndPgdataAreEachRejectedByOptInValidation()
    {
        var safeData = Path.Combine(Path.GetTempPath(), "not-created", "data");

        Assert.Throws<InvalidOperationException>(() => DisposablePostgresCluster.ValidateOptInTarget(
            "Host=127.0.0.1;Port=49357;Database=gymflow_codex_test_base;Username=gymflow_test;Password=synthetic;SSL Mode=Disable",
            "49357", safeData, "gymflow_codex_test_base", "gymflow_test"));
        Assert.Throws<InvalidOperationException>(() => DisposablePostgresCluster.ValidateOptInTarget(
            "Host=127.0.0.1;Port=55439;Database=issue130_restore_avelri;Username=issue130_restore;Password=synthetic;SSL Mode=Disable",
            "55439", safeData, "issue130_restore_avelri", "issue130_restore"));
        Assert.Throws<InvalidOperationException>(() => DisposablePostgresCluster.ValidateOptInTarget(
            "Host=127.0.0.1;Port=55439;Database=issue130_erp_work_20261101;Username=issue130_restore;Password=synthetic;SSL Mode=Disable",
            "55439", safeData, "issue130_erp_work_20261101", "issue130_restore"));
        Assert.Throws<InvalidOperationException>(() => DisposablePostgresCluster.ValidateOptInTarget(
            "Host=127.0.0.1;Port=55439;Database=gymflow_codex_test_base;Username=gymflow_test;Password=synthetic;SSL Mode=Disable",
            "55439", @"R:\Issue130\RestorePostgres17\data", "gymflow_codex_test_base", "gymflow_test"));
    }

    [Fact]
    public async Task IdentityVerificationFailsBeforeOpeningConnectionWithoutHarnessOwnedCluster()
    {
        await using var connection = new NpgsqlConnection(
            "Host=127.0.0.1;Port=49357;Database=issue130_restore_avelri;Username=issue130_restore");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DisposablePostgresCluster.VerifyIdentityAsync(
                connection,
                "issue130_restore_avelri",
                "issue130_restore",
                49357,
                ""));

        Assert.NotEmpty(exception.Message);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task CreateDatabaseFailsClosedBeforeOpeningConnectionWithoutHarnessOwnedCluster()
    {
        await using var connection = new NpgsqlConnection(
            "Host=127.0.0.1;Port=49357;Database=issue130_restore_avelri;Username=issue130_restore");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DisposablePostgresCluster.CreateDatabaseAsync(
                connection,
                $"gymflow_codex_test_{Guid.NewGuid():N}"));

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task DropDatabaseFailsClosedBeforeOpeningConnectionWithoutHarnessOwnedCluster()
    {
        await using var connection = new NpgsqlConnection(
            "Host=127.0.0.1;Port=49357;Database=issue130_restore_avelri;Username=issue130_restore");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DisposablePostgresCluster.DropDatabaseAsync(
                connection,
                $"gymflow_codex_test_{Guid.NewGuid():N}"));

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    [HarnessOwnedPostgresFact]
    public async Task HarnessStartsAndUsesOnlyItsPrivateTemporaryPostgresCluster()
    {
        var originalConnection = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION");
        var originalPort = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT");
        var originalDataDirectory = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA");
        var originalPostgresBin = Environment.GetEnvironmentVariable("GYMFLOW_POSTGRES_BIN");
        var placeholderData = Path.Combine(Path.GetTempPath(), "not-created", "data");
        Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION",
            "Host=127.0.0.1;Port=55439;Database=gymflow_codex_test_base;Username=gymflow_test;Password=synthetic;SSL Mode=Disable");
        Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT", "55439");
        Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA", placeholderData);

        NpgsqlConnection? admin = null;
        string? testDatabase = null;
        try
        {
            var connection = DisposablePostgresCluster.BuildConnectionString(
                "GYMFLOW_TEST_GUARD_CONNECTION",
                "GYMFLOW_TEST_GUARD_PORT",
                "gymflow_codex_test_base",
                "gymflow_test",
                "GYMFLOW_TEST_GUARD_DATA");
            var identity = DisposablePostgresCluster.CurrentOwnedIdentityForTests();

            Assert.Equal("127.0.0.1", connection.Host);
            Assert.False(DisposablePostgresCluster.IsReservedPort(connection.Port));
            Assert.NotEqual(49357, connection.Port);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),
                Path.GetFullPath(identity.DataDirectory), StringComparison.OrdinalIgnoreCase);
            Assert.False(DisposablePostgresCluster.IsProtectedDataDirectory(identity.DataDirectory));

            admin = new NpgsqlConnection(connection.ConnectionString);
            await DisposablePostgresCluster.OpenVerifiedConnectionAsync(admin);
            await DisposablePostgresCluster.VerifyIdentityAsync(
                admin,
                "gymflow_codex_test_base",
                "gymflow_test",
                identity.Port,
                "GYMFLOW_TEST_GUARD_DATA");

            testDatabase = DisposablePostgresCluster.NewDatabaseName("gymflow_codex_test_");
            await DisposablePostgresCluster.CreateDatabaseAsync(admin, testDatabase);
            connection.Database = testDatabase;
            await using (var testConnection = new NpgsqlConnection(connection.ConnectionString))
            {
                await DisposablePostgresCluster.RunWithVerifiedConnectionAsync(
                    testConnection,
                    () => Task.CompletedTask);
            }

            await DisposablePostgresCluster.DropDatabaseAsync(admin, testDatabase);
            testDatabase = null;
        }
        finally
        {
            if (admin is not null)
            {
                if (testDatabase is not null && admin.State == System.Data.ConnectionState.Open)
                {
                    await DisposablePostgresCluster.DropDatabaseAsync(admin, testDatabase, ifExists: true);
                }
                await admin.DisposeAsync();
            }

            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION", originalConnection);
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT", originalPort);
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA", originalDataDirectory);
            Environment.SetEnvironmentVariable("GYMFLOW_POSTGRES_BIN", originalPostgresBin);
        }
    }

    [HarnessOwnedPostgresFact]
    public async Task VerifiedConnectionCanReopenButCannotReuseProofAfterIdentitySubstitution()
    {
        var originalConnection = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION");
        var originalPort = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT");
        var originalDataDirectory = Environment.GetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA");
        const string optInPassword = "synthetic-guard-only";
        var dataDirectory = Path.Combine(Path.GetTempPath(), "not-created", "connection-proof");
        Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION",
            $"Host=127.0.0.1;Port=55439;Database=gymflow_codex_test_base;Username=gymflow_test;Password={optInPassword};SSL Mode=Disable");
        Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT", "55439");
        Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA", dataDirectory);

        try
        {
            var baseConnection = DisposablePostgresCluster.BuildConnectionString(
                "GYMFLOW_TEST_GUARD_CONNECTION",
                "GYMFLOW_TEST_GUARD_PORT",
                "gymflow_codex_test_base",
                "gymflow_test",
                "GYMFLOW_TEST_GUARD_DATA");
            var identity = DisposablePostgresCluster.CurrentOwnedIdentityForTests();
            var originalOwnedConnectionString = baseConnection.ConnectionString;
            await using var trusted = new NpgsqlConnection(originalOwnedConnectionString);

            await DisposablePostgresCluster.OpenVerifiedConnectionAsync(trusted);
            await trusted.CloseAsync();
            await DisposablePostgresCluster.RunWithVerifiedConnectionAsync(trusted, () => Task.CompletedTask);
            await trusted.CloseAsync();

            var incorrectCredential = new NpgsqlConnectionStringBuilder(originalOwnedConnectionString)
            {
                Password = "synthetic-wrong-credential"
            };
            await using (var invalid = new NpgsqlConnection(incorrectCredential.ConnectionString))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    DisposablePostgresCluster.RunWithVerifiedConnectionAsync(invalid, () => Task.CompletedTask));
                Assert.Contains("credential", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("synthetic-wrong-credential", exception.Message, StringComparison.Ordinal);
                Assert.Equal(System.Data.ConnectionState.Closed, invalid.State);
            }

            var otherPort = identity.Port == 65001 ? 65002 : 65001;
            var mutations = new (string Name, Action<NpgsqlConnectionStringBuilder> Apply)[]
            {
                ("host", value => value.Host = "localhost"),
                ("port", value => value.Port = otherPort),
                ("database", value => value.Database = "issue130_restore_avelri"),
                ("username", value => value.Username = "issue130_restore"),
                ("ApplicationName", value => value.ApplicationName = "external-connection")
            };

            foreach (var mutation in mutations)
            {
                var changed = new NpgsqlConnectionStringBuilder(originalOwnedConnectionString);
                mutation.Apply(changed);
                trusted.ConnectionString = changed.ConnectionString;
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    DisposablePostgresCluster.RunWithVerifiedConnectionAsync(trusted, () => Task.CompletedTask));
                Assert.NotEmpty(exception.Message);
                Assert.Equal(System.Data.ConnectionState.Closed, trusted.State);

                trusted.ConnectionString = originalOwnedConnectionString;
                await DisposablePostgresCluster.RunWithVerifiedConnectionAsync(trusted, () => Task.CompletedTask);
                await trusted.CloseAsync();
            }

            var redactedReplacement = trusted.ConnectionString;
            var replacementBuilder = new NpgsqlConnectionStringBuilder(redactedReplacement);
            Assert.True(string.IsNullOrEmpty(replacementBuilder.Password));
            await using (var replacement = new NpgsqlConnection(redactedReplacement))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    DisposablePostgresCluster.RunWithVerifiedConnectionAsync(replacement, () => Task.CompletedTask));
                Assert.Contains("credential", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(System.Data.ConnectionState.Closed, replacement.State);
            }

            var protectedTarget = new NpgsqlConnectionStringBuilder(originalOwnedConnectionString)
            {
                Database = "issue130_restore_avelri",
                Username = "issue130_restore"
            };
            await using (var protectedConnection = new NpgsqlConnection(protectedTarget.ConnectionString))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    DisposablePostgresCluster.RunWithVerifiedConnectionAsync(protectedConnection, () => Task.CompletedTask));
                Assert.Equal(System.Data.ConnectionState.Closed, protectedConnection.State);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_CONNECTION", originalConnection);
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_PORT", originalPort);
            Environment.SetEnvironmentVariable("GYMFLOW_TEST_GUARD_DATA", originalDataDirectory);
        }
    }
}

public sealed class HarnessOwnedPostgresFactAttribute : FactAttribute
{
    public HarnessOwnedPostgresFactAttribute()
    {
        if (!DisposablePostgresCluster.Postgres17ToolsAvailable())
        {
            Skip = "PostgreSQL 17 initdb, pg_ctl, and postgres are not available for a harness-owned temporary cluster.";
        }
    }
}
