using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class PhysicalAccessOverrideRepository
    : IPhysicalAccessOverrideRepository
{
    private readonly AppDbContext _context;

    public PhysicalAccessOverrideRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<PhysicalAccessOverride?>
        GetByStudentAsync(
            Guid studentId,
            Guid gymId)
    {
        return await _context.PhysicalAccessOverrides
            .Include(accessOverride => accessOverride.Student)
                .ThenInclude(student => student.User)
            .Include(accessOverride => accessOverride.ActorUser)
            .FirstOrDefaultAsync(accessOverride =>
                accessOverride.StudentId == studentId &&
                accessOverride.GymId == gymId &&
                accessOverride.Student.User.GymId == gymId &&
                accessOverride.ActorUser.GymId == gymId);
    }

    public async Task AddAsync(
        PhysicalAccessOverride accessOverride)
    {
        await _context.PhysicalAccessOverrides
            .AddAsync(accessOverride);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(
        PhysicalAccessOverride accessOverride)
    {
        _context.PhysicalAccessOverrides
            .Update(accessOverride);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(
        PhysicalAccessOverride accessOverride)
    {
        _context.PhysicalAccessOverrides
            .Remove(accessOverride);

        await _context.SaveChangesAsync();
    }
}