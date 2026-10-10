using GymFlow.AccessAgent.Adapters.Toletus;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Adapters;

public class ToletusLiteNet2AdapterStatusTests
{
    [Fact]
    public void GetRuntimeStatus_WhenConfiguredButNotConnected_ShouldReportDisconnected()
    {
        var options =
            Options.Create(
                new ToletusLiteNet2Options
                {
                    DeviceKey = "front-door",
                    Enabled = true,
                    Host = "192.168.0.50",
                    Port = 7878
                });

        var releaseControl =
            Substitute.For<
                IAccessReleaseControl>();

        using var adapter =
            new ToletusLiteNet2Adapter(
                options,
                NullLogger<
                    ToletusLiteNet2Adapter>
                    .Instance,
                releaseControl);

        var status =
            adapter.GetRuntimeStatus();

        Assert.Equal(
            "front-door",
            status.DeviceKey);

        Assert.Equal(
            "toletus-litenet2",
            status.ProviderKey);

        Assert.True(
            status.Enabled);

        Assert.False(
            status.Connected);

        Assert.Equal(
            "192.168.0.50:7878",
            status.Endpoint);
    }
}
