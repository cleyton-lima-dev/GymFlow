using System.Text.Json;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class AccessAgentControlService
{
    private readonly IAccessAgentRepository
        _accessAgentRepository;

    private readonly IAccessAgentAuditRepository
        _auditRepository;

    public AccessAgentControlService(
        IAccessAgentRepository accessAgentRepository,
        IAccessAgentAuditRepository auditRepository)
    {
        _accessAgentRepository = accessAgentRepository;
        _auditRepository = auditRepository;
    }

    public async Task<List<AccessAgentManagementResponse>>
        ListAsync(Guid gymId)
    {
        if (gymId == Guid.Empty)
            return [];

        var agents =
            await _accessAgentRepository
                .GetByGymAsync(gymId);

        return agents
            .Select(ToResponse)
            .ToList();
    }

    public async Task<AccessAgentManagementResponse?>
        SetReleaseEnabledAsync(
            Guid gymId,
            Guid actorUserId,
            Guid agentId,
            bool enabled)
    {
        if (gymId == Guid.Empty ||
            actorUserId == Guid.Empty ||
            agentId == Guid.Empty)
        {
            return null;
        }

        var agent =
            await _accessAgentRepository
                .GetByIdAsync(
                    agentId,
                    gymId);

        if (agent is null)
            return null;

        if (agent.ReleaseEnabled == enabled)
        {
            return ToResponse(agent);
        }

        var previousValues =
            JsonSerializer.Serialize(
                new
                {
                    agent.ReleaseEnabled,
                    agent.ConfigurationVersion
                });

        var now = DateTime.UtcNow;

        agent.ReleaseEnabled = enabled;
        agent.ConfigurationVersion =
            checked(agent.ConfigurationVersion + 1);
        agent.UpdatedAt = now;

        var newValues =
            JsonSerializer.Serialize(
                new
                {
                    agent.ReleaseEnabled,
                    agent.ConfigurationVersion
                });

        var auditLog =
            new AccessAgentAuditLog
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                ActorUserId = actorUserId,
                AccessAgentId = agent.Id,
                Action =
                    AccessAgentAuditAction
                        .ConfigurationChanged,
                PreviousValues = previousValues,
                NewValues = newValues,
                OccurredAt = now
            };

        await _auditRepository
            .StageAsync(auditLog);

        await _accessAgentRepository
            .UpdateAsync(agent);

        return ToResponse(agent);
    }

    public async Task<AccessAgentHeartbeatResponse?>
        HeartbeatAsync(
            Guid gymId,
            Guid agentId,
            long? appliedConfigurationVersion)
    {
        if (gymId == Guid.Empty ||
            agentId == Guid.Empty)
        {
            return null;
        }

        if (appliedConfigurationVersion.HasValue &&
            appliedConfigurationVersion.Value < 1)
        {
            throw new ArgumentException(
                "A versão aplicada da configuração deve ser maior que zero.");
        }

        var agent =
            await _accessAgentRepository
                .GetByIdAsync(
                    agentId,
                    gymId);

        if (agent is null ||
            !agent.IsActive)
        {
            return null;
        }

        if (appliedConfigurationVersion.HasValue &&
            appliedConfigurationVersion.Value >
                agent.ConfigurationVersion)
        {
            throw new ArgumentException(
                "O agente informou uma versão de configuração posterior à versão atual do servidor.");
        }

        var now = DateTime.UtcNow;

        agent.LastSeenAt = now;

        agent.AppliedConfigurationVersion =
            appliedConfigurationVersion;

        await _accessAgentRepository
            .UpdateAsync(agent);

        return new AccessAgentHeartbeatResponse
        {
            ReleaseEnabled = agent.ReleaseEnabled,
            ConfigurationVersion =
                agent.ConfigurationVersion,
            ServerTimeUtc = now
        };
    }

    private static AccessAgentManagementResponse
        ToResponse(AccessAgent agent)
    {
        return new AccessAgentManagementResponse
        {
            Id = agent.Id,
            GymId = agent.GymId,
            Name = agent.Name,
            MachineName = agent.MachineName,
            IsActive = agent.IsActive,
            ReleaseEnabled = agent.ReleaseEnabled,
            ConfigurationVersion =
                agent.ConfigurationVersion,
            AppliedConfigurationVersion =
                agent.AppliedConfigurationVersion,
            ConfigurationApplied =
                agent.AppliedConfigurationVersion.HasValue &&
                agent.AppliedConfigurationVersion.Value ==
                    agent.ConfigurationVersion,
            LastSeenAt = agent.LastSeenAt,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
