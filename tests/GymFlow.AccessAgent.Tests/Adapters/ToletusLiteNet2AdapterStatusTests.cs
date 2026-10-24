using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Adapters.Toletus;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Adapters;

public class ToletusLiteNet2AdapterStatusTests
{
    [Fact]
    public void GetRuntimeStatus_BeforeRemoteConfigurationLoads_ShouldReportDisabled()
    {
        var releaseControl =
            Substitute.For<IAccessReleaseControl>();

        using var adapter =
            CreateAdapter(
                releaseControl);

        var status =
            adapter.GetRuntimeStatus();

        Assert.Equal(
            "primary",
            status.DeviceKey);

        Assert.Equal(
            "toletus-litenet2",
            status.ProviderKey);

        Assert.False(
            status.Enabled);

        Assert.False(
            status.Connected);

        Assert.Null(
            status.Endpoint);
    }

    [Fact]
    public async Task StartAsync_WhenRemoteConfigurationIsDisabled_ShouldExposeRemoteConfiguration()
    {
        var releaseControl =
            Substitute.For<IAccessReleaseControl>();

        releaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    5,
                    [
                        new AgentDeviceConfiguration(
                            "front-door",
                            "toletus-litenet2",
                            false,
                            "192.168.0.50",
                            7878)
                    ]));

        using var adapter =
            CreateAdapter(
                releaseControl);

        using var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(3));

        var running =
            adapter.StartAsync(
                (_, _) =>
                    Task.FromResult(
                        new AccessDeviceDecision(
                            false,
                            "Test")),
                cancellation.Token);

        AccessDeviceRuntimeStatus? status =
            null;

        for (var attempt = 0;
             attempt < 40;
             attempt++)
        {
            status =
                adapter.GetRuntimeStatus();

            if (status.DeviceKey ==
                "front-door")
            {
                break;
            }

            await Task.Delay(25);
        }

        Assert.NotNull(
            status);

        Assert.Equal(
            "front-door",
            status.DeviceKey);

        Assert.True(
            status.Enabled == false);

        Assert.False(
            status.Connected);

        Assert.Equal(
            "192.168.0.50:7878",
            status.Endpoint);

        cancellation.Cancel();

        await running;
    }

    private static ToletusLiteNet2Adapter
        CreateAdapter(
            IAccessReleaseControl releaseControl)
    {
        var options =
            Options.Create(
                new ToletusLiteNet2Options
                {
                    ReconnectDelaySeconds = 1
                });

        return new ToletusLiteNet2Adapter(
            options,
            NullLogger<
                ToletusLiteNet2Adapter>
                .Instance,
            releaseControl,
            Substitute.For<IPhysicalPassageOfflineStore>(),
            TimeProvider.System);
    }
}
