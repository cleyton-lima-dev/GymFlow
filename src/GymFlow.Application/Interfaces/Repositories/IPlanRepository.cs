using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPlanRepository
{
    Task AddAsync(Plan plan);

    Task<Plan?> GetByIdAsync(Guid id, Guid gymId);

    Task<Plan?> GetByNameAsync(string name, Guid gymId);

    Task<List<Plan>> GetByGymAsync(Guid gymId, bool? isActive);

    Task UpdateAsync(Plan plan);
}