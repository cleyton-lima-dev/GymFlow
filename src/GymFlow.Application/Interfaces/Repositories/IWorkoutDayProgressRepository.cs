using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IWorkoutDayProgressRepository
{
    Task<WorkoutDayProgress?> GetByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task<bool> IsWorkoutExerciseInActiveDayAsync(
        Guid workoutExerciseId,
        Guid workoutDayId,
        Guid studentId,
        Guid gymId);

    Task<int> CountExercisesInDayAsync(
        Guid workoutDayId,
        Guid studentId,
        Guid gymId);

    Task AddProgressAsync(
        WorkoutDayProgress progress);

    Task AddCompletionAsync(
        WorkoutExerciseCompletion completion);

    void RemoveCompletion(
        WorkoutExerciseCompletion completion);

    void RemoveProgress(
        WorkoutDayProgress progress);

    Task SaveChangesAsync();
}