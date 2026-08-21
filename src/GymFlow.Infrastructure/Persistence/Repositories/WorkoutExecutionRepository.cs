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

    public async Task<List<WorkoutExecution>> GetLatestByWorkoutDayIdsAsync(
    IEnumerable<Guid> workoutDayIds)
    {
        var ids = workoutDayIds.ToList();

        if (ids.Count == 0)
            return new List<WorkoutExecution>();

        return await _context.WorkoutExecutions
            .AsNoTracking()
            .Where(x => ids.Contains(x.WorkoutDayId))
            .GroupBy(x => x.WorkoutDayId)
            .Select(group => group
                .OrderByDescending(x => x.CompletedAt)
                .First())
            .ToListAsync();
    }

    public async Task<bool> ExistsForWorkoutDayOnDateAsync(
    Guid workoutDayId,
    DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        return await _context.WorkoutExecutions
            .AsNoTracking()
            .AnyAsync(x =>
                x.WorkoutDayId == workoutDayId &&
                x.CompletedAt >= start &&
                x.CompletedAt < end);
    }

    public async Task<int> CountHistoryByStudentAsync(
    Guid studentId,
    Guid gymId)
    {
        return await _context.WorkoutExecutions
            .AsNoTracking()
            .Where(execution =>
                execution.WorkoutDay.Workout.StudentId == studentId &&
                execution.WorkoutDay.Workout.GymId == gymId)
            .CountAsync();
    }
}