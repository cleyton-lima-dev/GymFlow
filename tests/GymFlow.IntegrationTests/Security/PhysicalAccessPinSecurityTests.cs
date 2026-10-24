using GymFlow.Api.Controllers;
using GymFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace GymFlow.IntegrationTests.Security;

public class PhysicalAccessPinSecurityTests
{
    [Fact]
    public void PinLookupUsesTenantBoundKeyedHashAndRequiresSecret()
    {
        var key = Convert.ToBase64String(Enumerable.Repeat((byte)0xA7, 32).ToArray());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PhysicalAccess:PinLookupKey"] = key
            })
            .Build();
        var protector = new PhysicalAccessPinProtector(configuration);
        var firstGym = Guid.NewGuid();
        var secondGym = Guid.NewGuid();

        var lookup = protector.ComputeLookupHash(firstGym, "0007");
        Assert.StartsWith("pin:v1:", lookup);
        Assert.NotEqual("0007", lookup);
        Assert.Equal(lookup, protector.ComputeLookupHash(firstGym, "0007"));
        Assert.NotEqual(lookup, protector.ComputeLookupHash(secondGym, "0007"));
        Assert.Throws<ArgumentException>(() => protector.ComputeLookupHash(firstGym, "7"));
        Assert.Throws<InvalidOperationException>(() =>
            new PhysicalAccessPinProtector(new ConfigurationBuilder().Build())
                .ComputeLookupHash(firstGym, "0007"));
    }

    [Fact]
    public void PinIssuanceEndpointRequiresAdminRole()
    {
        var authorization = typeof(PhysicalAccessCredentialsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin", authorization.Roles);
    }
}
