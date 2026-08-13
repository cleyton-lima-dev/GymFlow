using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IExerciseRepository
{
    Task AddAsync(Exercise exercise);

    Task<Exercise?> GetByIdAsync(Guid id, Guid gymId);

    Task<List<Exercise>> GetAllByGymAsync(
    Guid gymId,
    string? search = null,
    string? muscleGroup = null,
    bool? isActive = null);

    Task<List<Exercise>> GetByIdsAsync(
    IEnumerable<Guid> ids,
    Guid gymId);

    Task<Exercise?> GetByNameAsync(string name, Guid gymId);

    Task UpdateAsync(Exercise exercise);
}