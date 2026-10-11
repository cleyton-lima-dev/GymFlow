using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class AccessAgentRepository : IAccessAgentRepository
{
    private readonly AppDbContext _context;

    public AccessAgentRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<AccessAgent?> GetByIdAsync(
        Guid agentId,
        Guid gymId)
    {
        return await _context.AccessAgents
            .FirstOrDefaultAsync(agent =>
                agent.Id == agentId &&
                agent.GymId == gymId);
    }

    public async Task<AccessAgent?> GetByMachineNameAsync(
        Guid gymId,
        string machineName)
    {
        return await _context.AccessAgents
            .FirstOrDefaultAsync(agent =>
                agent.GymId == gymId &&
                agent.MachineName == machineName);
    }

    public async Task<List<AccessAgent>> GetByGymAsync(
        Guid gymId)
    {
        return await _context.AccessAgents
            .Where(agent =>
                agent.GymId == gymId)
            .OrderBy(agent => agent.Name)
            .ToListAsync();
    }

    public async Task AddAsync(
        AccessAgent agent)
    {
        await _context.AccessAgents
            .AddAsync(agent);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(
        AccessAgent agent)
    {
        _context.AccessAgents.Update(agent);

        await _context.SaveChangesAsync();
    }
}
