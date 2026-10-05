using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class PhysicalAccessEventRepository
    : IPhysicalAccessEventRepository
{
    private readonly AppDbContext _context;

    public PhysicalAccessEventRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<PhysicalAccessEvent?> GetByRequestIdAsync(
        Guid gymId,
        Guid requestId)
    {
        return await _context.PhysicalAccessEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(accessEvent =>
                accessEvent.GymId == gymId &&
                accessEvent.RequestId == requestId);
    }

    public async Task<PhysicalAccessEvent> AddAsync(
        PhysicalAccessEvent accessEvent)
    {
        await _context.PhysicalAccessEvents
            .AddAsync(accessEvent);

        try
        {
            await _context.SaveChangesAsync();

            return accessEvent;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            _context.Entry(accessEvent).State =
                EntityState.Detached;

            var existingEvent =
                await _context.PhysicalAccessEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existing =>
                        existing.GymId ==
                            accessEvent.GymId &&
                        existing.RequestId ==
                            accessEvent.RequestId);

            if (existingEvent is not null)
                return existingEvent;

            throw;
        }
    }
}