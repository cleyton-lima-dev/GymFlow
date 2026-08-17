using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class WorkoutRepository : IWorkoutRepository
{
    private readonly AppDbContext _context;

    public WorkoutRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Workout workout)
    {
        await _context.Workouts.AddAsync(workout);
    }

    public async Task<Workout?> GetByIdAsync(
        Guid id,
        Guid gymId)
    {
        return await _context.Workouts
            .AsNoTracking()
            .Include(x => x.Days.OrderBy(d => d.Order))
                .ThenInclude(x => x.Exercises.OrderBy(e => e.Order))
                    .ThenInclude(x => x.Exercise)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.GymId == gymId);
    }

    public async Task<Workout?> GetActiveByStudentAsync(
        Guid studentId,
        Guid gymId)
    {
        return await _context.Workouts
            .AsNoTracking()
            .Include(x => x.Days.OrderBy(d => d.Order))
                .ThenInclude(x => x.Exercises.OrderBy(e => e.Order))
                    .ThenInclude(x => x.Exercise)
            .FirstOrDefaultAsync(x =>
                x.StudentId == studentId &&
                x.GymId == gymId &&
                x.IsActive);
    }

    public async Task<Workout?> GetForUpdateAsync(
         Guid id,
         Guid gymId)
    {
        return await _context.Workouts
            .Include(x => x.Days)
                .ThenInclude(x => x.Exercises)
            .Include(x => x.Days)
                .ThenInclude(x => x.Executions)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.GymId == gymId);
    }
    

    public async Task<Workout?> GetActiveForUpdateAsync(
        Guid studentId,
        Guid gymId)
    {
        return await _context.Workouts
            .FirstOrDefaultAsync(x =>
                x.StudentId == studentId &&
                x.GymId == gymId &&
                x.IsActive);
    }

    public async Task<List<Workout>> GetAllByStudentAsync(
        Guid studentId,
        Guid gymId)
    {
        return await _context.Workouts
            .AsNoTracking()
            .Where(x =>
                x.StudentId == studentId &&
                x.GymId == gymId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}