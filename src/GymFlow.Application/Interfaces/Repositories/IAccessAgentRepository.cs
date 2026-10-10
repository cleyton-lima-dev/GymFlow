using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IAccessAgentRepository
{
    Task<AccessAgent?> GetByIdAsync(
        Guid agentId,
        Guid gymId);

    Task<AccessAgent?> GetByMachineNameAsync(
        Guid gymId,
        string machineName);

    Task<List<AccessAgent>> GetByGymAsync(
        Guid gymId);

    Task AddAsync(AccessAgent agent);

    Task UpdateAsync(AccessAgent agent);
}
