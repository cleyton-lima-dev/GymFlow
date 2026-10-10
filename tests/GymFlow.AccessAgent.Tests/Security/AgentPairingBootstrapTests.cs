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
    public void TryCreateCredentials_WhenValuesAreValid_ShouldCreateCredentials()
    {
        var agentId =
            Guid.NewGuid();

        var gymId =
            Guid.NewGuid();

        var result =
            AgentPairingBootstrap
                .TryCreateCredentials(
                    agentId.ToString(),
                    gymId.ToString(),
                    "secret-value",
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
    public void TryCreateCredentials_WhenAgentIdIsInvalid_ShouldReject()
    {
        var result =
            AgentPairingBootstrap
                .TryCreateCredentials(
                    "invalid",
                    Guid.NewGuid().ToString(),
                    "secret-value",
                    "http://localhost:5137",
                    out var credentials,
                    out var error);

        Assert.False(result);
        Assert.Null(credentials);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreateCredentials_WhenRemoteHttpIsUsed_ShouldReject()
    {
        var result =
            AgentPairingBootstrap
                .TryCreateCredentials(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    "secret-value",
                    "http://example.com",
                    out var credentials,
                    out var error);

        Assert.False(result);
        Assert.Null(credentials);
        Assert.Equal(
            "HTTPS é obrigatório fora do ambiente local.",
            error);
    }

    [Fact]
    public void TryCreateCredentials_WhenHttpsIsUsed_ShouldAccept()
    {
        var result =
            AgentPairingBootstrap
                .TryCreateCredentials(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    "secret-value",
                    "https://api.avelri.com",
                    out var credentials,
                    out var error);

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Null(error);
    }
}
