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

    public async Task<(List<PhysicalAccessEvent> Items, int TotalCount)>
        GetPagedAsync(
            Guid gymId,
            Guid? studentId,
            GymFlow.Domain.Enums.PhysicalAccessDecision? decision,
            GymFlow.Domain.Enums.PhysicalAccessDecisionSource? source,
            DateTime? occurredFrom,
            DateTime? occurredTo,
            int skip,
            int take)
    {
        var query = _context.PhysicalAccessEvents
            .AsNoTracking()
            .Include(accessEvent => accessEvent.Student)
                .ThenInclude(student => student!.User)
            .Where(accessEvent => accessEvent.GymId == gymId);

        if (studentId.HasValue)
            query = query.Where(accessEvent =>
                accessEvent.StudentId == studentId.Value);

        if (decision.HasValue)
            query = query.Where(accessEvent =>
                accessEvent.Decision == decision.Value);

        if (source.HasValue)
            query = query.Where(accessEvent =>
                accessEvent.Source == source.Value);

        if (occurredFrom.HasValue)
            query = query.Where(accessEvent =>
                accessEvent.OccurredAt >= occurredFrom.Value);

        if (occurredTo.HasValue)
            query = query.Where(accessEvent =>
                accessEvent.OccurredAt < occurredTo.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(accessEvent => accessEvent.OccurredAt)
            .ThenByDescending(accessEvent => accessEvent.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }
}
