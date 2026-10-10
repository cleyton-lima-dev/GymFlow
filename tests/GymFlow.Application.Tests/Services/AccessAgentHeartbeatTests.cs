using GymFlow.Application.DTOs.AccessAgents;
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
    public async Task HeartbeatAsync_WithRuntimeStatus_ShouldPersistStatusAndAppliedVersion()
    {
        var agent =
            CreateAgent();

        agent.ConfigurationVersion = 3;

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                agent.GymId)
            .Returns(agent);

        var devices =
            new[]
            {
                new AccessAgentDeviceStatusDto
                {
                    DeviceKey = "primary",
                    ProviderKey =
                        "toletus-litenet2",
                    Enabled = true,
                    Connected = true,
                    Endpoint =
                        "192.168.0.50:7878"
                }
            };

        var lastOfflineSyncAt =
            DateTime.UtcNow.AddMinutes(-2);

        var lastFailureAt =
            DateTime.UtcNow.AddMinutes(-1);

        var result =
            await _service
                .HeartbeatAsync(
                    agent.GymId,
                    agent.Id,
                    3,
                    2,
                    devices,
                    lastOfflineSyncAt,
                    lastFailureAt,
                    "OfflineSync.ApiUnavailable");

        Assert.NotNull(result);
        Assert.True(result.ReleaseEnabled);

        Assert.Equal(
            3,
            agent.AppliedConfigurationVersion);

        Assert.Equal(
            2,
            agent.PendingOfflineEvents);

        Assert.Contains(
            "toletus-litenet2",
            agent.DeviceStatusesJson);

        Assert.Equal(
            lastOfflineSyncAt,
            agent.LastOfflineSyncAt);

        Assert.Equal(
            lastFailureAt,
            agent.LastFailureAt);

        Assert.Equal(
            "OfflineSync.ApiUnavailable",
            agent.LastFailureCode);

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
                3,
                0,
                []));

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
                    2,
                    0,
                    []);

        Assert.NotNull(result);

        Assert.Equal(
            2,
            agent.AppliedConfigurationVersion);

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
                    null,
                    0,
                    []);

        Assert.NotNull(result);

        Assert.Null(
            agent.AppliedConfigurationVersion);

        await _agentRepository
            .Received(1)
            .UpdateAsync(agent);
    }

    [Fact]
    public async Task HeartbeatAsync_WithNegativePendingEvents_ShouldReject()
    {
        var agent =
            CreateAgent();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.HeartbeatAsync(
                agent.GymId,
                agent.Id,
                1,
                -1,
                []));

        await _agentRepository
            .DidNotReceive()
            .UpdateAsync(
                Arg.Any<AccessAgent>());
    }

    [Fact]
    public async Task HeartbeatAsync_WithDuplicateDeviceKeys_ShouldReject()
    {
        var agent =
            CreateAgent();

        var devices =
            new[]
            {
                new AccessAgentDeviceStatusDto
                {
                    DeviceKey = "primary",
                    ProviderKey = "provider-a",
                    Enabled = true,
                    Connected = true
                },
                new AccessAgentDeviceStatusDto
                {
                    DeviceKey = "PRIMARY",
                    ProviderKey = "provider-b",
                    Enabled = true,
                    Connected = false
                }
            };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.HeartbeatAsync(
                agent.GymId,
                agent.Id,
                1,
                0,
                devices));

        await _agentRepository
            .DidNotReceive()
            .UpdateAsync(
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
            SecretHash = "hash",
            IsActive = true,
            ReleaseEnabled = true,
            ConfigurationVersion = 1
        };
    }
}
