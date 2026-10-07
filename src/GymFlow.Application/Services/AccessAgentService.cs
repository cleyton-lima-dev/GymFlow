using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class AccessAgentService
{
    private readonly IAccessAgentRepository
        _accessAgentRepository;

    private readonly IAccessAgentSecretService
        _secretService;

    private readonly IAccessAgentTokenService
    _tokenService;

    public AccessAgentService(
    IAccessAgentRepository accessAgentRepository,
    IAccessAgentSecretService secretService,
    IAccessAgentTokenService tokenService)
    {
        _accessAgentRepository = accessAgentRepository;
        _secretService = secretService;
        _tokenService = tokenService;
    }

    public async Task<CreateAccessAgentResponse?> CreateAsync(
        Guid gymId,
        CreateAccessAgentRequest request)
    {
        if (gymId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.MachineName))
        {
            return null;
        }

        var name = request.Name.Trim();
        var machineName = request.MachineName.Trim();

        PersistenceTextPolicy.ValidateMaxLength(
            name,
            PersistenceTextPolicy.AccessAgentNameMaxLength,
            "O nome do agente");

        PersistenceTextPolicy.ValidateMaxLength(
            machineName,
            PersistenceTextPolicy.AccessAgentMachineNameMaxLength,
            "O nome da máquina");

        var existingAgent =
            await _accessAgentRepository
                .GetByMachineNameAsync(
                    gymId,
                    machineName);

        if (existingAgent is not null)
        {
            return null;
        }

        var secret =
            _secretService.GenerateSecret();

        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            MachineName = machineName,
            SecretHash = _secretService.Hash(secret),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _accessAgentRepository.AddAsync(agent);

        return new CreateAccessAgentResponse
        {
            AgentId = agent.Id,
            GymId = agent.GymId,
            Name = agent.Name,
            MachineName = agent.MachineName,
            Secret = secret
        };
    }

    public async Task<AccessAgentLoginResponse?> LoginAsync(
    AccessAgentLoginRequest request)
    {
        if (request.AgentId == Guid.Empty ||
            request.GymId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Secret))
        {
            return null;
        }

        var agent =
            await _accessAgentRepository.GetByIdAsync(
                request.AgentId,
                request.GymId);

        if (agent is null || !agent.IsActive)
        {
            return null;
        }

        var secretIsValid =
            _secretService.Verify(
                request.Secret,
                agent.SecretHash);

        if (!secretIsValid)
        {
            return null;
        }

        agent.LastSeenAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;

        await _accessAgentRepository.UpdateAsync(agent);

        var token =
            _tokenService.GenerateToken(agent);

        return new AccessAgentLoginResponse
        {
            AgentId = agent.Id,
            GymId = agent.GymId,
            Name = agent.Name,
            Token = token
        };
    }
}