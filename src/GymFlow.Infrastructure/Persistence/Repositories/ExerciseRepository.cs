using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class ExerciseRepository : IExerciseRepository
{
    private readonly AppDbContext _context;

    public ExerciseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Exercise exercise)
    {
        await _context.Exercises.AddAsync(exercise);
        await _context.SaveChangesAsync();
    }

    public async Task<Exercise?> GetByIdAsync(Guid id, Guid gymId)
    {
        return await _context.Exercises
            .FirstOrDefaultAsync(exercise =>
                exercise.Id == id &&
                exercise.GymId == gymId);
    }

    public async Task<(List<Exercise> Items, int TotalCount)> GetPagedByGymAsync(
    Guid gymId,
    string? search,
    string? muscleGroup,
    bool? isActive,
    int skip,
    int take)
    {
        var query = _context.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.GymId == gymId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(exercise =>
                EF.Functions.ILike(
                    exercise.Name,
                    $"%{searchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(muscleGroup))
        {
            var muscleGroupTerm = muscleGroup.Trim();

            query = query.Where(exercise =>
                EF.Functions.ILike(
                    exercise.MuscleGroup,
                    muscleGroupTerm));
        }

        if (isActive.HasValue)
        {
            query = query.Where(exercise =>
                exercise.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(exercise => exercise.Name)
            .ThenBy(exercise => exercise.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Exercise?> GetByNameAsync(string name, Guid gymId)
    {
        return await _context.Exercises
            .FirstOrDefaultAsync(exercise =>
                exercise.GymId == gymId &&
                exercise.Name == name);
    }

    public async Task UpdateAsync(Exercise exercise)
    {
        _context.Exercises.Update(exercise);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Exercise>> GetByIdsAsync(
    IEnumerable<Guid> ids,
    Guid gymId)
    {
        return await _context.Exercises
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.GymId == gymId)
            .ToListAsync();
    }
}