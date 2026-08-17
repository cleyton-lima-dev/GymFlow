using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class WorkoutExecutionRepository
    : IWorkoutExecutionRepository
{
    private readonly AppDbContext _context;

    public WorkoutExecutionRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsActiveWorkoutDayForStudentAsync(
        Guid workoutDayId,
        Guid studentId,
        Guid gymId)
    {
        return await _context.WorkoutDays
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == workoutDayId &&
                x.Workout.StudentId == studentId &&
                x.Workout.GymId == gymId &&
                x.Workout.IsActive);
    }

    public async Task AddAsync(
        WorkoutExecution execution)
    {
        await _context.WorkoutExecutions
            .AddAsync(execution);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<List<WorkoutExecution>> GetHistoryByStudentAsync(
    Guid studentId,
    Guid gymId,
    int skip,
    int take)
    {
        return await _context.WorkoutExecutions
            .AsNoTracking()
            .Where(x =>
                x.WorkoutDay.Workout.StudentId == studentId &&
                x.WorkoutDay.Workout.GymId == gymId)
            .Include(x => x.WorkoutDay)
                .ThenInclude(x => x.Workout)
            .OrderByDescending(x => x.CompletedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }
}