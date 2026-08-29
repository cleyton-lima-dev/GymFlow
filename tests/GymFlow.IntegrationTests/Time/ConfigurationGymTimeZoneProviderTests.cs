using GymFlow.Application.Exceptions;
using GymFlow.Infrastructure.Time;
using Microsoft.Extensions.Configuration;

namespace GymFlow.IntegrationTests.Time;

public class ConfigurationGymTimeZoneProviderTests
{
    [Fact]
    public void GetTimeZone_WhenGymHasNoConfiguration_ShouldThrowSpecificException()
    {
        var configuration =
            new ConfigurationBuilder().Build();

        var provider =
            new ConfigurationGymTimeZoneProvider(
                configuration);

        var gymId = Guid.NewGuid();

        var exception =
            Assert.Throws<GymTimeZoneNotConfiguredException>(
                () => provider.GetTimeZone(gymId));

        Assert.Contains(
            gymId.ToString(),
            exception.Message);

        Assert.IsNotType<KeyNotFoundException>(
            exception);
    }
}
