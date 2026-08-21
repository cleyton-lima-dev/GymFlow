using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IExerciseRepository
{
    Task AddAsync(Exercise exercise);

    Task<Exercise?> GetByIdAsync(Guid id, Guid gymId);

    Task<(List<Exercise> Items, int TotalCount)> GetPagedByGymAsync(
    Guid gymId,
    string? search,
    string? muscleGroup,
    bool? isActive,
    int skip,
    int take);

    Task<List<Exercise>> GetByIdsAsync(
    IEnumerable<Guid> ids,
    Guid gymId);

    Task<Exercise?> GetByNameAsync(string name, Guid gymId);

    Task UpdateAsync(Exercise exercise);
}