using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IWorkoutExecutionRepository
{
    Task<bool> IsActiveWorkoutDayForStudentAsync(
        Guid workoutDayId,
        Guid studentId,
        Guid gymId);

    Task AddAsync(WorkoutExecution execution);

    Task<List<WorkoutExecution>> GetHistoryByStudentAsync(
    Guid studentId,
    Guid gymId,
    int skip,
    int take);

    Task<int> CountHistoryByStudentAsync(
    Guid studentId,
    Guid gymId);

    Task<List<WorkoutExecution>> GetLatestByWorkoutDayIdsAsync(
    IEnumerable<Guid> workoutDayIds);

    Task SaveChangesAsync();

    Task<bool> ExistsForWorkoutDayOnDateAsync(
    Guid workoutDayId,
    DateTime date);
}