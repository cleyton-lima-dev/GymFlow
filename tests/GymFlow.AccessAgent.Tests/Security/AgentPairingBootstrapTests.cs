using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;

namespace GymFlow.AccessAgent.Tests.Security;

public class AgentPairingBootstrapTests
{
    [Fact]
    public void IsPairingRequested_WhenFlagExists_ShouldReturnTrue()
    {
        var result =
            AgentPairingBootstrap
                .IsPairingRequested(
                    ["--PAIR"]);

        Assert.True(result);
    }

    [Fact]
    public void TryValidateApiBaseUrl_WhenLocalHttp_ShouldAccept()
    {
        var result =
            AgentPairingBootstrap
                .TryValidateApiBaseUrl(
                    "http://localhost:5137/",
                    out var baseUrl,
                    out var error);

        Assert.True(result);
        Assert.Null(error);

        Assert.Equal(
            "http://localhost:5137",
            baseUrl);
    }

    [Fact]
    public void TryValidateApiBaseUrl_WhenRemoteHttp_ShouldReject()
    {
        var result =
            AgentPairingBootstrap
                .TryValidateApiBaseUrl(
                    "http://example.com",
                    out var baseUrl,
                    out var error);

        Assert.False(result);
        Assert.Null(baseUrl);

        Assert.Equal(
            "HTTPS é obrigatório fora do ambiente local.",
            error);
    }

    [Fact]
    public void TryCreateCredentials_WhenPairingResultIsValid_ShouldCreateCredentials()
    {
        var agentId =
            Guid.NewGuid();

        var gymId =
            Guid.NewGuid();

        var pairingResult =
            new PairAccessAgentResult(
                agentId,
                gymId,
                "Avelri Access Agent",
                "RECEPCAO-01",
                "secret-value");

        var result =
            AgentPairingBootstrap
                .TryCreateCredentials(
                    pairingResult,
                    "http://localhost:5137/",
                    out var credentials,
                    out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.NotNull(credentials);

        Assert.Equal(
            agentId,
            credentials.AgentId);

        Assert.Equal(
            gymId,
            credentials.GymId);

        Assert.Equal(
            "secret-value",
            credentials.Secret);

        Assert.Equal(
            "http://localhost:5137",
            credentials.ApiBaseUrl);
    }

    [Fact]
    public void TryCreateCredentials_WhenResultIsInvalid_ShouldReject()
    {
        var pairingResult =
            new PairAccessAgentResult(
                Guid.Empty,
                Guid.NewGuid(),
                "Agent",
                "RECEPCAO-01",
                "secret-value");

        var result =
            AgentPairingBootstrap
                .TryCreateCredentials(
                    pairingResult,
                    "http://localhost:5137",
                    out var credentials,
                    out var error);

        Assert.False(result);
        Assert.Null(credentials);
        Assert.NotNull(error);
    }
}
