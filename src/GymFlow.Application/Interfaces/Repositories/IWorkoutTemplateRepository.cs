using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IWorkoutTemplateRepository
{
    Task AddAsync(WorkoutTemplate workoutTemplate);

    Task<WorkoutTemplate?> GetByIdAsync(Guid id, Guid gymId);

    Task<(List<WorkoutTemplate> Items, int TotalCount)> GetPagedByGymAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    int skip,
    int take);

    Task<bool> ExistsByNameAsync(Guid gymId, string name, Guid? ignoreId = null);

    Task SaveChangesAsync();

    Task<WorkoutTemplate?> GetForUpdateAsync(Guid id, Guid gymId);

    Task RemoveDaysAsync(IEnumerable<WorkoutTemplateDay> days);

    void AddDays(IEnumerable<WorkoutTemplateDay> days);

    Task<bool> SetActiveStatusAsync(
    Guid id,
    Guid gymId,
    bool isActive);
}