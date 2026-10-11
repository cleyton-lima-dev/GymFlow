using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class AccessAgentControlServiceTests
{
    private readonly IAccessAgentRepository
        _agentRepository;

    private readonly IAccessAgentAuditRepository
        _auditRepository;

    private readonly AccessAgentControlService
        _service;

    public AccessAgentControlServiceTests()
    {
        _agentRepository =
            Substitute.For<IAccessAgentRepository>();

        _auditRepository =
            Substitute.For<IAccessAgentAuditRepository>();

        _service =
            new AccessAgentControlService(
                _agentRepository,
                _auditRepository);
    }

    [Fact]
    public async Task SetReleaseEnabledAsync_WhenValueChanges_ShouldIncrementVersionAndAudit()
    {
        var gymId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();

        var agent =
            new AccessAgent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Catraca principal",
                MachineName = "RECEPCAO-01",
                SecretHash = "hash",
                IsActive = true,
                ReleaseEnabled = false,
                ConfigurationVersion = 1,
                LastSeenAt = DateTime.UtcNow,
                DeviceStatusesJson =
                    """
                    [
                      {
                        "DeviceKey": "primary",
                        "ProviderKey": "toletus-litenet2",
                        "Enabled": true,
                        "Connected": true,
                        "Endpoint": "192.168.0.50:7878"
                      }
                    ]
                    """
            };

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                gymId)
            .Returns(agent);

        var result =
            await _service
                .SetReleaseEnabledAsync(
                    gymId,
                    actorUserId,
                    agent.Id,
                    true);

        Assert.NotNull(result);
        Assert.True(result.ReleaseEnabled);
        Assert.Equal(
            2,
            result.ConfigurationVersion);

        await _auditRepository
            .Received(1)
            .StageAsync(
                Arg.Is<AccessAgentAuditLog>(
                    audit =>
                        audit.GymId == gymId &&
                        audit.ActorUserId ==
                            actorUserId &&
                        audit.AccessAgentId ==
                            agent.Id &&
                        audit.Action ==
                            AccessAgentAuditAction
                                .ConfigurationChanged &&
                        audit.PreviousValues
                            .Contains("false") &&
                        audit.NewValues
                            .Contains("true")));

        await _agentRepository
            .Received(1)
            .UpdateAsync(agent);
    }

    [Fact]
    public async Task SetReleaseEnabledAsync_WhenAgentIsOffline_ShouldRejectEnable()
    {
        var gymId =
            Guid.NewGuid();

        var agent =
            new AccessAgent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Catraca principal",
                MachineName = "RECEPCAO-01",
                SecretHash = "hash",
                IsActive = true,
                ReleaseEnabled = false,
                ConfigurationVersion = 1,
                LastSeenAt =
                    DateTime.UtcNow.AddMinutes(-10),
                DeviceStatusesJson =
                    """
                    [
                      {
                        "DeviceKey": "primary",
                        "ProviderKey": "toletus-litenet2",
                        "Enabled": true,
                        "Connected": true,
                        "Endpoint": "192.168.0.50:7878"
                      }
                    ]
                    """
            };

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                gymId)
            .Returns(agent);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service
                .SetReleaseEnabledAsync(
                    gymId,
                    Guid.NewGuid(),
                    agent.Id,
                    true));

        await _auditRepository
            .DidNotReceiveWithAnyArgs()
            .StageAsync(default!);

        await _agentRepository
            .DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }

    [Fact]
    public async Task SetReleaseEnabledAsync_WhenNoDeviceIsConnected_ShouldRejectEnable()
    {
        var gymId =
            Guid.NewGuid();

        var agent =
            new AccessAgent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Catraca principal",
                MachineName = "RECEPCAO-01",
                SecretHash = "hash",
                IsActive = true,
                ReleaseEnabled = false,
                ConfigurationVersion = 1,
                LastSeenAt = DateTime.UtcNow,
                DeviceStatusesJson =
                    """
                    [
                      {
                        "DeviceKey": "primary",
                        "ProviderKey": "toletus-litenet2",
                        "Enabled": true,
                        "Connected": false,
                        "Endpoint": "192.168.0.50:7878"
                      }
                    ]
                    """
            };

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                gymId)
            .Returns(agent);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service
                .SetReleaseEnabledAsync(
                    gymId,
                    Guid.NewGuid(),
                    agent.Id,
                    true));

        await _auditRepository
            .DidNotReceiveWithAnyArgs()
            .StageAsync(default!);

        await _agentRepository
            .DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }
    [Fact]
    public async Task SetReleaseEnabledAsync_WhenValueIsUnchanged_ShouldNotAuditOrUpdate()
    {
        var gymId = Guid.NewGuid();

        var agent =
            new AccessAgent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Catraca principal",
                MachineName = "RECEPCAO-01",
                SecretHash = "hash",
                ReleaseEnabled = true,
                ConfigurationVersion = 4
            };

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                gymId)
            .Returns(agent);

        var result =
            await _service
                .SetReleaseEnabledAsync(
                    gymId,
                    Guid.NewGuid(),
                    agent.Id,
                    true);

        Assert.NotNull(result);
        Assert.True(result.ReleaseEnabled);
        Assert.Equal(
            4,
            result.ConfigurationVersion);

        await _auditRepository
            .DidNotReceiveWithAnyArgs()
            .StageAsync(default!);

        await _agentRepository
            .DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }

    [Fact]
    public async Task SetReleaseEnabledAsync_WhenAgentBelongsToAnotherGym_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentRepository
            .GetByIdAsync(
                agentId,
                gymId)
            .Returns((AccessAgent?)null);

        var result =
            await _service
                .SetReleaseEnabledAsync(
                    gymId,
                    Guid.NewGuid(),
                    agentId,
                    true);

        Assert.Null(result);

        await _auditRepository
            .DidNotReceiveWithAnyArgs()
            .StageAsync(default!);

        await _agentRepository
            .DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }
}
