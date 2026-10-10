using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Tests.Services;

public class SqliteAccessReleaseControlTests
{
    [Fact]
    public async Task IsReleaseEnabledAsync_WhenSettingDoesNotExist_ShouldBeDisabled()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var control =
                CreateControl(
                    databasePath);

            var result =
                await control.IsReleaseEnabledAsync(
                    CancellationToken.None);

            Assert.False(result);
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    [Fact]
    public async Task ReleaseState_ShouldPersistAcrossInstances()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var firstControl =
                CreateControl(
                    databasePath);

            await firstControl.EnableAsync(
                CancellationToken.None);

            var secondControl =
                CreateControl(
                    databasePath);

            Assert.True(
                await secondControl.IsReleaseEnabledAsync(
                    CancellationToken.None));

            await secondControl.DisableAsync(
                CancellationToken.None);

            var thirdControl =
                CreateControl(
                    databasePath);

            Assert.False(
                await thirdControl.IsReleaseEnabledAsync(
                    CancellationToken.None));
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    private static SqliteAccessReleaseControl CreateControl(
        string databasePath)
    {
        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    DatabasePath = databasePath
                });

        return new SqliteAccessReleaseControl(
            options);
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"avelri-release-control-{Guid.NewGuid():N}.db");
    }

    private static void Cleanup(
        string databasePath)
    {
        SqliteConnection.ClearAllPools();

        DeleteIfExists(
            databasePath);

        DeleteIfExists(
            $"{databasePath}-wal");

        DeleteIfExists(
            $"{databasePath}-shm");
    }

    private static void DeleteIfExists(
        string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
