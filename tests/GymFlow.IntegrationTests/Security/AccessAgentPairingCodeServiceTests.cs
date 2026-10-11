using GymFlow.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace GymFlow.IntegrationTests.Security;

public class AccessAgentPairingCodeServiceTests
{
    [Fact]
    public void Hash_ShouldNormalizeEquivalentCodes()
    {
        var service =
            CreateService(
                "test-key-with-at-least-32-bytes-123456");

        var first =
            service.Hash(
                "ABCD-EFGH");

        var second =
            service.Hash(
                " abcd efgh ");

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void Hash_WithDifferentApplicationKeys_ShouldProduceDifferentHashes()
    {
        var firstService =
            CreateService(
                "first-test-key-with-at-least-32-bytes");

        var secondService =
            CreateService(
                "second-test-key-with-at-least-32-bytes");

        var first =
            firstService.Hash(
                "ABCD-EFGH");

        var second =
            secondService.Hash(
                "ABCD-EFGH");

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void Hash_ShouldBeDeterministicForSameKeyAndCode()
    {
        var service =
            CreateService(
                "test-key-with-at-least-32-bytes-123456");

        var first =
            service.Hash(
                "ABCD-EFGH");

        var second =
            service.Hash(
                "ABCD-EFGH");

        Assert.Equal(
            first,
            second);

        Assert.Equal(
            44,
            first.Length);
    }

    [Fact]
    public void GenerateCode_ShouldUseExpectedFormat()
    {
        var service =
            CreateService(
                "test-key-with-at-least-32-bytes-123456");

        var code =
            service.GenerateCode();

        Assert.Equal(
            9,
            code.Length);

        Assert.Equal(
            '-',
            code[4]);

        Assert.All(
            code.Where(
                character =>
                    character != '-'),
            character =>
                Assert.Contains(
                    character,
                    "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"));
    }

    [Fact]
    public void Constructor_WhenJwtKeyIsMissing_ShouldFailClosed()
    {
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

        Assert.Throws<InvalidOperationException>(
            () =>
                new AccessAgentPairingCodeService(
                    configuration));
    }

    private static AccessAgentPairingCodeService
        CreateService(
            string jwtKey)
    {
        var values =
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] =
                    jwtKey
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    values)
                .Build();

        return new AccessAgentPairingCodeService(
            configuration);
    }
}