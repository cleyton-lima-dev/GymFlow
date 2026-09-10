using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class WorkoutDayProgressRepository
    : IWorkoutDayProgressRepository
{
    private readonly AppDbContext _context;

    public WorkoutDayProgressRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkoutDayProgress?> GetByStudentAsync(
        Guid studentId,
        Guid gymId)
    {
        return await _context.WorkoutDayProgresses
            .Include(x => x.CompletedExercises)
            .Include(x => x.WorkoutDay)
                .ThenInclude(x => x.Workout)
            .FirstOrDefaultAsync(x =>
                x.StudentId == studentId &&
                x.Student.User.GymId == gymId);
    }

    public async Task<bool> IsWorkoutExerciseInActiveDayAsync(
        Guid workoutExerciseId,
        Guid workoutDayId,
        Guid studentId,
        Guid gymId)
    {
        return await _context.WorkoutExercises
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == workoutExerciseId &&
                x.WorkoutDayId == workoutDayId &&
                x.WorkoutDay.Workout.StudentId == studentId &&
                x.WorkoutDay.Workout.GymId == gymId &&
                x.WorkoutDay.Workout.IsActive);
    }

    public async Task<int> CountExercisesInDayAsync(
        Guid workoutDayId,
        Guid studentId,
        Guid gymId)
    {
        return await _context.WorkoutExercises
            .AsNoTracking()
            .CountAsync(x =>
                x.WorkoutDayId == workoutDayId &&
                x.WorkoutDay.Workout.StudentId == studentId &&
                x.WorkoutDay.Workout.GymId == gymId &&
                x.WorkoutDay.Workout.IsActive);
    }

    public async Task AddProgressAsync(
        WorkoutDayProgress progress)
    {
        await _context.WorkoutDayProgresses
            .AddAsync(progress);
    }

    public async Task AddCompletionAsync(
        WorkoutExerciseCompletion completion)
    {
        await _context.WorkoutExerciseCompletions
            .AddAsync(completion);
    }

    public void RemoveCompletion(
        WorkoutExerciseCompletion completion)
    {
        _context.WorkoutExerciseCompletions
            .Remove(completion);
    }

    public void RemoveProgress(
        WorkoutDayProgress progress)
    {
        _context.WorkoutDayProgresses
            .Remove(progress);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}