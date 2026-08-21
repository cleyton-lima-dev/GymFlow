using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class WorkoutTemplateRepository : IWorkoutTemplateRepository
{
    private readonly AppDbContext _context;

    public WorkoutTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WorkoutTemplate workoutTemplate)
    {
        await _context.WorkoutTemplates.AddAsync(workoutTemplate);
        await _context.SaveChangesAsync();
    }

    public async Task<WorkoutTemplate?> GetByIdAsync(Guid id, Guid gymId)
    {
        return await _context.WorkoutTemplates
            .AsNoTracking()
            .Include(x => x.Days.OrderBy(d => d.Order))
                .ThenInclude(x => x.Exercises.OrderBy(e => e.Order))
                    .ThenInclude(x => x.Exercise)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.GymId == gymId);
    }

    public async Task<(List<WorkoutTemplate> Items, int TotalCount)> GetPagedByGymAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    int skip,
    int take)
    {
        var query = _context.WorkoutTemplates
            .AsNoTracking()
            .Where(x => x.GymId == gymId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(x =>
                EF.Functions.ILike(
                    x.Name,
                    $"%{searchTerm}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x =>
                x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(
        Guid gymId,
        string name,
        Guid? ignoreId = null)
    {
        return await _context.WorkoutTemplates
            .AnyAsync(x =>
                x.GymId == gymId &&
                x.Name == name &&
                (!ignoreId.HasValue || x.Id != ignoreId.Value));
    }

    public async Task UpdateAsync(WorkoutTemplate workoutTemplate)
    {
        await _context.SaveChangesAsync();
    }

    public async Task<WorkoutTemplate?> GetForUpdateAsync(
    Guid id,
    Guid gymId)
    {
        return await _context.WorkoutTemplates
            .Include(x => x.Days)
                .ThenInclude(x => x.Exercises)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.GymId == gymId);
    }

    public async Task<bool> SetActiveStatusAsync(
    Guid id,
    Guid gymId,
    bool isActive)
    {
        var template = await _context.WorkoutTemplates
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.GymId == gymId);

        if (template is null)
            return false;

        template.IsActive = isActive;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task RemoveDaysAsync(
    IEnumerable<WorkoutTemplateDay> days)
    {
        _context.WorkoutTemplateDays.RemoveRange(days);

        await _context.SaveChangesAsync();
    }

    public void AddDays(IEnumerable<WorkoutTemplateDay> days)
    {
        _context.WorkoutTemplateDays.AddRange(days);
    }
}