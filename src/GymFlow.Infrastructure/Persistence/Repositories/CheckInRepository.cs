using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class CheckInRepository :
    ICheckInRepository
{
    private readonly AppDbContext _context;

    public CheckInRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<CheckIn?> GetByRequestIdAsync(
        Guid gymId,
        Guid requestId)
    {
        return await _context.CheckIns
            .AsNoTracking()
            .FirstOrDefaultAsync(checkIn =>
                checkIn.GymId == gymId &&
                checkIn.RequestId == requestId);
    }

    public async Task<CheckIn?> GetByPhysicalAccessEventIdAsync(
        Guid gymId,
        Guid physicalAccessEventId)
    {
        return await _context.CheckIns
            .AsNoTracking()
            .FirstOrDefaultAsync(checkIn =>
                checkIn.GymId == gymId &&
                checkIn.PhysicalAccessEventId ==
                    physicalAccessEventId);
    }

    public async Task<CheckIn> AddAsync(
        CheckIn checkIn)
    {
        await _context.CheckIns.AddAsync(
            checkIn);

        try
        {
            await _context.SaveChangesAsync();

            return checkIn;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState:
                    PostgresErrorCodes.UniqueViolation
            })
        {
            _context.Entry(checkIn).State =
                EntityState.Detached;

            var existingByRequest =
                await GetByRequestIdAsync(
                    checkIn.GymId,
                    checkIn.RequestId);

            if (existingByRequest is not null)
                return existingByRequest;

            if (checkIn.PhysicalAccessEventId.HasValue)
            {
                var existingByAccessEvent =
                    await GetByPhysicalAccessEventIdAsync(
                        checkIn.GymId,
                        checkIn.PhysicalAccessEventId.Value);

                if (existingByAccessEvent is not null)
                    return existingByAccessEvent;
            }

            throw;
        }
    }

    public async Task<(List<CheckIn> Items, int TotalCount)>
        GetPagedAsync(
            Guid gymId,
            Guid? studentId,
            DateTime? occurredFrom,
            DateTime? occurredTo,
            int skip,
            int take)
    {
        var query =
            _context.CheckIns
                .AsNoTracking()
                .Include(checkIn =>
                    checkIn.Student)
                .ThenInclude(student =>
                    student.User)
                .Where(checkIn =>
                    checkIn.GymId == gymId);

        if (studentId.HasValue)
        {
            query =
                query.Where(checkIn =>
                    checkIn.StudentId ==
                        studentId.Value);
        }

        if (occurredFrom.HasValue)
        {
            query =
                query.Where(checkIn =>
                    checkIn.OccurredAt >=
                        occurredFrom.Value);
        }

        if (occurredTo.HasValue)
        {
            query =
                query.Where(checkIn =>
                    checkIn.OccurredAt <
                        occurredTo.Value);
        }

        var totalCount =
            await query.CountAsync();

        var items =
            await query
                .OrderByDescending(checkIn =>
                    checkIn.OccurredAt)
                .ThenByDescending(checkIn =>
                    checkIn.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

        return (items, totalCount);
    }
}