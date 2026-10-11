using System.Text.Json;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Persistence;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class AccessAgentPairingService
{
    private static readonly TimeSpan PairingCodeLifetime =
        TimeSpan.FromMinutes(10);

    private readonly IAccessAgentRepository
        _accessAgentRepository;

    private readonly IAccessAgentPairingCodeRepository
        _pairingCodeRepository;

    private readonly IAccessAgentAuditRepository
        _auditRepository;

    private readonly IAccessAgentPairingCodeService
        _pairingCodeService;

    private readonly IAccessAgentSecretService
        _secretService;

    private readonly IApplicationTransaction
        _transaction;

    public AccessAgentPairingService(
        IAccessAgentRepository accessAgentRepository,
        IAccessAgentPairingCodeRepository pairingCodeRepository,
        IAccessAgentAuditRepository auditRepository,
        IAccessAgentPairingCodeService pairingCodeService,
        IAccessAgentSecretService secretService,
        IApplicationTransaction transaction)
    {
        _accessAgentRepository =
            accessAgentRepository;

        _pairingCodeRepository =
            pairingCodeRepository;

        _auditRepository =
            auditRepository;

        _pairingCodeService =
            pairingCodeService;

        _secretService =
            secretService;

        _transaction =
            transaction;
    }

    public async Task<CreateAccessAgentPairingCodeResponse?>
        CreateCodeAsync(
            Guid gymId,
            Guid userId)
    {
        if (gymId == Guid.Empty ||
            userId == Guid.Empty)
        {
            return null;
        }

        var now =
            DateTime.UtcNow;

        var code =
            _pairingCodeService
                .GenerateCode();

        var pairingCode =
            new AccessAgentPairingCode
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                CreatedByUserId = userId,
                CodeHash =
                    _pairingCodeService
                        .Hash(code),
                CreatedAt = now,
                ExpiresAt =
                    now.Add(
                        PairingCodeLifetime)
            };

        CreateAccessAgentPairingCodeResponse?
            result = null;

        await _transaction
            .ExecuteAsync(
                async () =>
                {
                    await _pairingCodeRepository
                        .ReplaceActiveAsync(
                            pairingCode,
                            now);

                    result =
                        new CreateAccessAgentPairingCodeResponse
                        {
                            Code = code,
                            ExpiresAt =
                                pairingCode.ExpiresAt
                        };
                });

        return result;
    }

    public async Task<PairAccessAgentResponse?>
        PairAsync(
            PairAccessAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.Code) ||
            string.IsNullOrWhiteSpace(
                request.MachineName))
        {
            return null;
        }

        var machineName =
            request.MachineName.Trim();

        PersistenceTextPolicy.ValidateMaxLength(
            machineName,
            PersistenceTextPolicy
                .AccessAgentMachineNameMaxLength,
            "O nome da máquina");

        var now =
            DateTime.UtcNow;

        var codeHash =
            _pairingCodeService
                .Hash(request.Code);

        PairAccessAgentResponse?
            result = null;

        await _transaction
            .ExecuteAsync(
                async () =>
                {
                    var pairingCode =
                        await _pairingCodeRepository
                            .TryConsumeAsync(
                                codeHash,
                                now);

                    if (pairingCode is null)
                    {
                        return;
                    }

                    var secret =
                        _secretService
                            .GenerateSecret();

                    var secretHash =
                        _secretService
                            .Hash(secret);

                    var existingAgent =
                        await _accessAgentRepository
                            .GetByMachineNameAsync(
                                pairingCode.GymId,
                                machineName);

                    AccessAgent agent;

                    if (existingAgent is null)
                    {
                        agent =
                            new AccessAgent
                            {
                                Id = Guid.NewGuid(),
                                GymId =
                                    pairingCode.GymId,
                                Name =
                                    "Avelri Access Agent",
                                MachineName =
                                    machineName,
                                SecretHash =
                                    secretHash,
                                CredentialVersion = 1,
                                IsActive = true,
                                ReleaseEnabled = false,
                                ConfigurationVersion = 1,
                                CreatedAt = now
                            };

                        var auditLog =
                            new AccessAgentAuditLog
                            {
                                Id = Guid.NewGuid(),
                                GymId = agent.GymId,
                                ActorUserId =
                                    pairingCode.CreatedByUserId,
                                AccessAgentId =
                                    agent.Id,
                                Action =
                                    AccessAgentAuditAction.Paired,
                                PreviousValues =
                                    "{}",
                                NewValues =
                                    JsonSerializer.Serialize(
                                        new
                                        {
                                            agent.MachineName,
                                            agent.CredentialVersion,
                                            agent.IsActive,
                                            agent.ReleaseEnabled,
                                            agent.ConfigurationVersion
                                        }),
                                OccurredAt =
                                    now
                            };

                        await _auditRepository
                            .StageAsync(
                                auditLog);

                        await _accessAgentRepository
                            .AddAsync(
                                agent);
                    }
                    else
                    {
                        agent =
                            existingAgent;

                        var previousValues =
                            JsonSerializer.Serialize(
                                new
                                {
                                    agent.MachineName,
                                    agent.CredentialVersion,
                                    agent.IsActive,
                                    agent.ReleaseEnabled,
                                    agent.ConfigurationVersion
                                });

                        agent.SecretHash =
                            secretHash;

                        agent.CredentialVersion =
                            checked(
                                agent.CredentialVersion + 1);

                        agent.IsActive =
                            true;

                        // Repareamento sempre volta
                        // para estado físico seguro.
                        agent.ReleaseEnabled =
                            false;

                        agent.ConfigurationVersion =
                            checked(
                                agent.ConfigurationVersion + 1);

                        agent.AppliedConfigurationVersion =
                            null;

                        agent.PendingOfflineEvents =
                            0;

                        agent.DeviceStatusesJson =
                            null;

                        agent.LastOfflineSyncAt =
                            null;

                        agent.LastFailureAt =
                            null;

                        agent.LastFailureCode =
                            null;

                        agent.LastSeenAt =
                            null;

                        agent.UpdatedAt =
                            now;

                        var auditLog =
                            new AccessAgentAuditLog
                            {
                                Id = Guid.NewGuid(),
                                GymId = agent.GymId,
                                ActorUserId =
                                    pairingCode.CreatedByUserId,
                                AccessAgentId =
                                    agent.Id,
                                Action =
                                    AccessAgentAuditAction.Repaired,
                                PreviousValues =
                                    previousValues,
                                NewValues =
                                    JsonSerializer.Serialize(
                                        new
                                        {
                                            agent.MachineName,
                                            agent.CredentialVersion,
                                            agent.IsActive,
                                            agent.ReleaseEnabled,
                                            agent.ConfigurationVersion
                                        }),
                                OccurredAt =
                                    now
                            };

                        await _auditRepository
                            .StageAsync(
                                auditLog);

                        await _accessAgentRepository
                            .UpdateAsync(
                                agent);
                    }

                    result =
                        new PairAccessAgentResponse
                        {
                            AgentId =
                                agent.Id,
                            GymId =
                                agent.GymId,
                            Name =
                                agent.Name,
                            MachineName =
                                agent.MachineName,
                            Secret =
                                secret
                        };
                });

        return result;
    }
}
