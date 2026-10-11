using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class AccessAgentServiceTests
{
    private readonly IAccessAgentRepository
        _accessAgentRepository;

    private readonly IAccessAgentSecretService
        _secretService;

    private readonly IAccessAgentTokenService
        _tokenService;

    private readonly AccessAgentService
        _service;

    public AccessAgentServiceTests()
    {
        _accessAgentRepository =
            Substitute.For<IAccessAgentRepository>();

        _secretService =
            Substitute.For<IAccessAgentSecretService>();

        _tokenService =
            Substitute.For<IAccessAgentTokenService>();

        _service = new AccessAgentService(
            _accessAgentRepository,
            _secretService,
            _tokenService);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateAgentAndReturnRawSecret()
    {
        var gymId = Guid.NewGuid();

        var request = new CreateAccessAgentRequest
        {
            Name = "  Catraca principal  ",
            MachineName = "  RECEPCAO-01  "
        };

        _accessAgentRepository
            .GetByMachineNameAsync(
                gymId,
                "RECEPCAO-01")
            .Returns((AccessAgent?)null);

        _secretService
            .GenerateSecret()
            .Returns("raw-secret");

        _secretService
            .Hash("raw-secret")
            .Returns("hashed-secret");

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.NotNull(result);
        Assert.Equal(gymId, result.GymId);
        Assert.Equal("Catraca principal", result.Name);
        Assert.Equal("RECEPCAO-01", result.MachineName);
        Assert.Equal("raw-secret", result.Secret);

        await _accessAgentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<AccessAgent>(agent =>
                    agent.GymId == gymId &&
                    agent.Name == "Catraca principal" &&
                    agent.MachineName == "RECEPCAO-01" &&
                    agent.SecretHash == "hashed-secret" &&
                    agent.IsActive));

        _secretService
            .Received(1)
            .GenerateSecret();

        _secretService
            .Received(1)
            .Hash("raw-secret");
    }

    [Fact]
    public async Task CreateAsync_WhenMachineAlreadyExists_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();

        var request = new CreateAccessAgentRequest
        {
            Name = "Catraca principal",
            MachineName = "RECEPCAO-01"
        };

        _accessAgentRepository
            .GetByMachineNameAsync(
                gymId,
                request.MachineName)
            .Returns(new AccessAgent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Agente existente",
                MachineName = request.MachineName,
                SecretHash = "hash",
                IsActive = true
            });

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.Null(result);

        _secretService
            .DidNotReceive()
            .GenerateSecret();

        await _accessAgentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<AccessAgent>());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnTokenAndUpdateLastSeen()
    {
        var agent = CreateAgent();

        _accessAgentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        _secretService
            .Verify(
                "raw-secret",
                agent.SecretHash)
            .Returns(true);

        _tokenService
            .GenerateToken(agent)
            .Returns("agent-token");

        var request = new AccessAgentLoginRequest
        {
            AgentId = agent.Id,
            GymId = agent.GymId,
            Secret = "raw-secret"
        };

        var result =
            await _service.LoginAsync(request);

        Assert.NotNull(result);
        Assert.Equal(agent.Id, result.AgentId);
        Assert.Equal(agent.GymId, result.GymId);
        Assert.Equal(agent.Name, result.Name);
        Assert.Equal("agent-token", result.Token);

        Assert.NotNull(agent.LastSeenAt);
        Assert.NotNull(agent.UpdatedAt);

        await _accessAgentRepository
            .Received(1)
            .UpdateAsync(agent);

        _tokenService
            .Received(1)
            .GenerateToken(agent);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidSecret_ShouldReturnNull()
    {
        var agent = CreateAgent();

        _accessAgentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        _secretService
            .Verify(
                "wrong-secret",
                agent.SecretHash)
            .Returns(false);

        var request = new AccessAgentLoginRequest
        {
            AgentId = agent.Id,
            GymId = agent.GymId,
            Secret = "wrong-secret"
        };

        var result =
            await _service.LoginAsync(request);

        Assert.Null(result);

        await _accessAgentRepository
            .DidNotReceive()
            .UpdateAsync(
                Arg.Any<AccessAgent>());

        _tokenService
            .DidNotReceive()
            .GenerateToken(
                Arg.Any<AccessAgent>());
    }

    [Fact]
    public async Task LoginAsync_WhenAgentIsInactive_ShouldReturnNull()
    {
        var agent = CreateAgent();
        agent.IsActive = false;

        _accessAgentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        var request = new AccessAgentLoginRequest
        {
            AgentId = agent.Id,
            GymId = agent.GymId,
            Secret = "raw-secret"
        };

        var result =
            await _service.LoginAsync(request);

        Assert.Null(result);

        _secretService
            .DidNotReceive()
            .Verify(
                Arg.Any<string>(),
                Arg.Any<string>());

        _tokenService
            .DidNotReceive()
            .GenerateToken(
                Arg.Any<AccessAgent>());
    }

    [Fact]
    public async Task LoginAsync_WithDifferentGym_ShouldReturnNull()
    {
        var agent = CreateAgent();
        var differentGymId = Guid.NewGuid();

        _accessAgentRepository
            .GetByIdAsync(
                agent.Id,
                differentGymId)
            .Returns((AccessAgent?)null);

        var request = new AccessAgentLoginRequest
        {
            AgentId = agent.Id,
            GymId = differentGymId,
            Secret = "raw-secret"
        };

        var result =
            await _service.LoginAsync(request);

        Assert.Null(result);

        _secretService
            .DidNotReceive()
            .Verify(
                Arg.Any<string>(),
                Arg.Any<string>());

        _tokenService
            .DidNotReceive()
            .GenerateToken(
                Arg.Any<AccessAgent>());
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("MachineName")]
    public async Task CreateAsync_WhenPersistedTextExceedsDatabaseLimit_ShouldThrow(
        string field)
    {
        var request = new CreateAccessAgentRequest
        {
            Name = "Catraca principal",
            MachineName = "RECEPCAO-01"
        };

        if (field == "Name")
        {
            request.Name =
                new string('A', 151);
        }
        else
        {
            request.MachineName =
                new string('M', 201);
        }

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _accessAgentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<AccessAgent>());
    }

    private static AccessAgent CreateAgent()
    {
        return new AccessAgent
        {
            Id = Guid.NewGuid(),
            GymId = Guid.NewGuid(),
            Name = "Catraca principal",
            MachineName = "RECEPCAO-01",
            SecretHash = "hashed-secret",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}