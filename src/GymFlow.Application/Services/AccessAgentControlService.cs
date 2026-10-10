using System.Text.Json;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class AccessAgentControlService
{
    private const int MaxReportedDevices = 20;
    private const int MaxDeviceKeyLength = 100;
    private const int MaxProviderKeyLength = 100;
    private const int MaxEndpointLength = 200;
    private const int MaxFailureCodeLength = 100;

    private static readonly TimeSpan AgentOnlineThreshold =
        TimeSpan.FromMinutes(3);

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
            long? appliedConfigurationVersion,
            int pendingOfflineEvents,
            IReadOnlyCollection<AccessAgentDeviceStatusDto>? devices,
            DateTime? lastOfflineSyncAt = null,
            DateTime? lastFailureAt = null,
            string? lastFailureCode = null)
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

        if (pendingOfflineEvents < 0)
        {
            throw new ArgumentException(
                "A quantidade de eventos offline pendentes não pode ser negativa.");
        }

        devices ??= [];

        ValidateDevices(devices);

        var normalizedFailureCode =
            string.IsNullOrWhiteSpace(lastFailureCode)
                ? null
                : lastFailureCode.Trim();

        if (lastFailureAt.HasValue !=
            (normalizedFailureCode is not null))
        {
            throw new ArgumentException(
                "A última falha deve informar data e código em conjunto.");
        }

        if (normalizedFailureCode is not null &&
            normalizedFailureCode.Length >
                MaxFailureCodeLength)
        {
            throw new ArgumentException(
                "O código da última falha excede o tamanho permitido.");
        }

        var normalizedLastOfflineSyncAt =
            lastOfflineSyncAt?.ToUniversalTime();

        var normalizedLastFailureAt =
            lastFailureAt?.ToUniversalTime();

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

        agent.PendingOfflineEvents =
            pendingOfflineEvents;

        agent.DeviceStatusesJson =
            JsonSerializer.Serialize(devices);

        if (normalizedLastOfflineSyncAt.HasValue &&
            (!agent.LastOfflineSyncAt.HasValue ||
             normalizedLastOfflineSyncAt.Value >
                agent.LastOfflineSyncAt.Value))
        {
            agent.LastOfflineSyncAt =
                normalizedLastOfflineSyncAt.Value;
        }

        if (normalizedLastFailureAt.HasValue &&
            (!agent.LastFailureAt.HasValue ||
             normalizedLastFailureAt.Value >
                agent.LastFailureAt.Value))
        {
            agent.LastFailureAt =
                normalizedLastFailureAt.Value;

            agent.LastFailureCode =
                normalizedFailureCode;
        }

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

    private static void ValidateDevices(
        IReadOnlyCollection<AccessAgentDeviceStatusDto> devices)
    {
        if (devices.Count > MaxReportedDevices)
        {
            throw new ArgumentException(
                $"O Agent não pode reportar mais de {MaxReportedDevices} dispositivos.");
        }

        var deviceKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var device in devices)
        {
            var deviceKey =
                device.DeviceKey?.Trim() ??
                string.Empty;

            var providerKey =
                device.ProviderKey?.Trim() ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(deviceKey) ||
                deviceKey.Length > MaxDeviceKeyLength)
            {
                throw new ArgumentException(
                    "O identificador local do dispositivo é inválido.");
            }

            if (string.IsNullOrWhiteSpace(providerKey) ||
                providerKey.Length > MaxProviderKeyLength)
            {
                throw new ArgumentException(
                    "O provedor do dispositivo é inválido.");
            }

            if (!deviceKeys.Add(deviceKey))
            {
                throw new ArgumentException(
                    "O Agent reportou dispositivos com identificadores locais duplicados.");
            }

            if (device.Endpoint is not null &&
                device.Endpoint.Length >
                    MaxEndpointLength)
            {
                throw new ArgumentException(
                    "O endpoint do dispositivo excede o tamanho permitido.");
            }

            if (!device.Enabled &&
                device.Connected)
            {
                throw new ArgumentException(
                    "Um dispositivo desabilitado não pode ser reportado como conectado.");
            }

            device.DeviceKey =
                deviceKey;

            device.ProviderKey =
                providerKey;

            device.Endpoint =
                string.IsNullOrWhiteSpace(
                    device.Endpoint)
                    ? null
                    : device.Endpoint.Trim();
        }
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
            IsOnline =
                agent.IsActive &&
                agent.LastSeenAt.HasValue &&
                DateTime.UtcNow -
                    agent.LastSeenAt.Value <=
                        AgentOnlineThreshold,
            PendingOfflineEvents =
                agent.PendingOfflineEvents,
            Devices =
                DeserializeDeviceStatuses(
                    agent.DeviceStatusesJson),
            LastOfflineSyncAt =
                agent.LastOfflineSyncAt,
            LastFailureAt =
                agent.LastFailureAt,
            LastFailureCode =
                agent.LastFailureCode,
            LastSeenAt = agent.LastSeenAt,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }

    private static IReadOnlyList<AccessAgentDeviceStatusDto>
        DeserializeDeviceStatuses(
            string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<
                       List<AccessAgentDeviceStatusDto>>(
                       json) ??
                   [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
