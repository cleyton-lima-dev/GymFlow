using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Tests.Services;

public class SqliteAccessReleaseControlTests
{
    [Fact]
    public async Task GetConfigurationAsync_WhenSettingDoesNotExist_ShouldBeDisabledAndUnversioned()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var control =
                CreateControl(
                    databasePath);

            var result =
                await control.GetConfigurationAsync(
                    CancellationToken.None);

            Assert.False(
                result.ReleaseEnabled);

            Assert.Null(
                result.ConfigurationVersion);

            Assert.False(
                await control.IsReleaseEnabledAsync(
                    CancellationToken.None));
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    [Fact]
    public async Task ApplyConfigurationAsync_ShouldPersistStateAndVersionAcrossInstances()
    {
        var databasePath =
            CreateDatabasePath();

        try
        {
            var firstControl =
                CreateControl(
                    databasePath);

            await firstControl
                .ApplyConfigurationAsync(
                    true,
                    3,
                    [
                        new AgentDeviceConfiguration(
                            "primary",
                            "toletus-litenet2",
                            false,
                            "192.168.0.50",
                            7878)
                    ],
                    CancellationToken.None);

            var secondControl =
                CreateControl(
                    databasePath);

            var state =
                await secondControl
                    .GetConfigurationAsync(
                        CancellationToken.None);

            Assert.True(
                state.ReleaseEnabled);

            Assert.Equal(
                3,
                state.ConfigurationVersion);

            var device =
                Assert.Single(
                    state.DesiredDevices);

            Assert.Equal(
                "primary",
                device.DeviceKey);

            Assert.Equal(
                "192.168.0.50",
                device.Host);

            Assert.Equal(
                7878,
                device.Port);

            Assert.True(
                await secondControl
                    .IsReleaseEnabledAsync(
                        CancellationToken.None));

            await secondControl
                .ApplyConfigurationAsync(
                    false,
                    4,
                    [],
                    CancellationToken.None);

            var thirdControl =
                CreateControl(
                    databasePath);

            var updatedState =
                await thirdControl
                    .GetConfigurationAsync(
                        CancellationToken.None);

            Assert.False(
                updatedState.ReleaseEnabled);

            Assert.Equal(
                4,
                updatedState.ConfigurationVersion);

            Assert.Empty(
                updatedState.DesiredDevices);

            Assert.False(
                await thirdControl
                    .IsReleaseEnabledAsync(
                        CancellationToken.None));
        }
        finally
        {
            Cleanup(
                databasePath);
        }
    }

    private static SqliteAccessReleaseControl
        CreateControl(
            string databasePath)
    {
        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    DatabasePath =
                        databasePath
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
