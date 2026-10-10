using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class AccessAgentHeartbeatTests
{
    private readonly IAccessAgentRepository
        _agentRepository;

    private readonly AccessAgentControlService
        _service;

    public AccessAgentHeartbeatTests()
    {
        _agentRepository =
            Substitute.For<IAccessAgentRepository>();

        var auditRepository =
            Substitute.For<IAccessAgentAuditRepository>();

        _service =
            new AccessAgentControlService(
                _agentRepository,
                auditRepository);
    }

    [Fact]
    public async Task HeartbeatAsync_WithAppliedVersion_ShouldUpdateLastSeenAndAppliedVersion()
    {
        var agent =
            CreateAgent();

        agent.ConfigurationVersion = 3;

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        var result =
            await _service
                .HeartbeatAsync(
                    agent.GymId,
                    agent.Id,
                    3);

        Assert.NotNull(result);
        Assert.True(result.ReleaseEnabled);
        Assert.Equal(
            3,
            result.ConfigurationVersion);

        Assert.Equal(
            3,
            agent.AppliedConfigurationVersion);

        Assert.NotNull(
            agent.LastSeenAt);

        await _agentRepository
            .Received(1)
            .UpdateAsync(agent);
    }

    [Fact]
    public async Task HeartbeatAsync_WithFutureVersion_ShouldReject()
    {
        var agent =
            CreateAgent();

        agent.ConfigurationVersion = 2;

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.HeartbeatAsync(
                agent.GymId,
                agent.Id,
                3));

        await _agentRepository
            .DidNotReceive()
            .UpdateAsync(
                Arg.Any<AccessAgent>());
    }

    [Fact]
    public async Task HeartbeatAsync_WithOlderReportedVersion_ShouldReflectCurrentAgentState()
    {
        var agent =
            CreateAgent();

        agent.ConfigurationVersion = 4;
        agent.AppliedConfigurationVersion = 3;

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        var result =
            await _service
                .HeartbeatAsync(
                    agent.GymId,
                    agent.Id,
                    2);

        Assert.NotNull(result);

        Assert.Equal(
            2,
            agent.AppliedConfigurationVersion);

        Assert.NotNull(
            agent.LastSeenAt);

        await _agentRepository
            .Received(1)
            .UpdateAsync(agent);
    }

    [Fact]
    public async Task HeartbeatAsync_WhenAgentReportsNoAppliedVersion_ShouldClearPreviousConfirmation()
    {
        var agent =
            CreateAgent();

        agent.ConfigurationVersion = 4;
        agent.AppliedConfigurationVersion = 4;

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        var result =
            await _service
                .HeartbeatAsync(
                    agent.GymId,
                    agent.Id,
                    null);

        Assert.NotNull(result);
        Assert.Null(
            agent.AppliedConfigurationVersion);

        await _agentRepository
            .Received(1)
            .UpdateAsync(agent);
    }
    private static AccessAgent CreateAgent()
    {
        return new AccessAgent
        {
            Id = Guid.NewGuid(),
            GymId = Guid.NewGuid(),
            Name = "Catraca principal",
            MachineName = "RECEPCAO-01",
            SecretHash = "hash",
            IsActive = true,
            ReleaseEnabled = true,
            ConfigurationVersion = 1
        };
    }
}
