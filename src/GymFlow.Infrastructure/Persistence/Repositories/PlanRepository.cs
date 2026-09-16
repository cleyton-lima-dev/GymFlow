using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _context;

    public PlanRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Plan plan)
    {
        await _context.Plans.AddAsync(plan);
        await _context.SaveChangesAsync();
    }

    public async Task<Plan?> GetByIdAsync(Guid id, Guid gymId)
    {
        return await _context.Plans
            .FirstOrDefaultAsync(plan =>
                plan.Id == id &&
                plan.GymId == gymId);
    }

    public async Task<Plan?> GetByNameAsync(string name, Guid gymId)
    {
        return await _context.Plans
            .FirstOrDefaultAsync(plan =>
                plan.GymId == gymId &&
                plan.Name == name);
    }

    public async Task<List<Plan>> GetByGymAsync(Guid gymId, bool? isActive)
    {
        var query = _context.Plans
            .AsNoTracking()
            .Where(plan => plan.GymId == gymId);

        if (isActive.HasValue)
        {
            query = query.Where(plan =>
                plan.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(plan => plan.Name)
            .ThenBy(plan => plan.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(Plan plan)
    {
        _context.Plans.Update(plan);
        await _context.SaveChangesAsync();
    }
}