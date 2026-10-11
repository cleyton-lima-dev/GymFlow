using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class AccessAgentDeviceConfigurationTests
{
    private readonly IAccessAgentRepository _agentRepository;
    private readonly IAccessAgentAuditRepository _auditRepository;
    private readonly AccessAgentControlService _service;

    public AccessAgentDeviceConfigurationTests()
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
    public async Task SetDevicesAsync_WhenConfigurationChanges_ShouldDisableReleaseIncrementVersionAndAudit()
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
                .SetDevicesAsync(
                    gymId,
                    actorUserId,
                    agent.Id,
                    [
                        new AccessAgentDeviceConfigurationDto
                        {
                            DeviceKey = " primary ",
                            ProviderKey = "toletus-litenet2",
                            Enabled = true,
                            Host = "192.168.0.50",
                            Port = 7878
                        }
                    ]);

        Assert.NotNull(result);
        Assert.False(result.ReleaseEnabled);
        Assert.Equal(
            5,
            result.ConfigurationVersion);

        var device =
            Assert.Single(
                result.DesiredDevices);

        Assert.Equal(
            "primary",
            device.DeviceKey);

        Assert.Equal(
            "192.168.0.50",
            device.Host);

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
                                .ConfigurationChanged));

        await _agentRepository
            .Received(1)
            .UpdateAsync(agent);
    }

    [Fact]
    public async Task SetDevicesAsync_WhenConfigurationIsUnchanged_ShouldNotUpdate()
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
                ConfigurationVersion = 3,
                DeviceConfigurationJson =
                    """
                    [{"DeviceKey":"primary","ProviderKey":"toletus-litenet2","Enabled":false,"Host":"192.168.0.50","Port":7878}]
                    """
            };

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                gymId)
            .Returns(agent);

        var result =
            await _service
                .SetDevicesAsync(
                    gymId,
                    Guid.NewGuid(),
                    agent.Id,
                    [
                        new AccessAgentDeviceConfigurationDto
                        {
                            DeviceKey = "primary",
                            ProviderKey = "toletus-litenet2",
                            Enabled = false,
                            Host = "192.168.0.50",
                            Port = 7878
                        }
                    ]);

        Assert.NotNull(result);

        Assert.Equal(
            3,
            result.ConfigurationVersion);

        await _auditRepository
            .DidNotReceiveWithAnyArgs()
            .StageAsync(default!);

        await _agentRepository
            .DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }

    [Fact]
    public async Task SetDevicesAsync_WhenProviderIsUnsupported_ShouldReject()
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
                ConfigurationVersion = 1
            };

        _agentRepository
            .GetByIdAsync(
                agent.Id,
                gymId)
            .Returns(agent);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service
                .SetDevicesAsync(
                    gymId,
                    Guid.NewGuid(),
                    agent.Id,
                    [
                        new AccessAgentDeviceConfigurationDto
                        {
                            DeviceKey = "primary",
                            ProviderKey = "unsupported",
                            Enabled = false,
                            Port = 7878
                        }
                    ]));

        await _agentRepository
            .DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }
}