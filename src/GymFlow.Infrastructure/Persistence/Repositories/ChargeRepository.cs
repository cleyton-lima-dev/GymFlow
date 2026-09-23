using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class ChargeRepository : IChargeRepository
{
    private readonly AppDbContext _context;

    public ChargeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Charge charge)
    {
        await _context.Charges.AddAsync(charge);
        await _context.SaveChangesAsync();
    }

    public async Task<Charge?> GetByIdAsync(
        Guid chargeId,
        Guid gymId)
    {
        return await _context.Charges
            .Include(charge => charge.Enrollment)
                .ThenInclude(enrollment => enrollment.Student)
                    .ThenInclude(student => student.User)
            .Include(charge => charge.Enrollment)
                .ThenInclude(enrollment => enrollment.Plan)
            .FirstOrDefaultAsync(charge =>
                charge.Id == chargeId &&
                charge.Enrollment.Student.User.GymId == gymId &&
                charge.Enrollment.Plan.GymId == gymId);
    }

    public async Task<Charge?> GetByEnrollmentIdAsync(
        Guid enrollmentId,
        Guid gymId)
    {
        return await _context.Charges
            .Include(charge => charge.Enrollment)
                .ThenInclude(enrollment => enrollment.Student)
                    .ThenInclude(student => student.User)
            .Include(charge => charge.Enrollment)
                .ThenInclude(enrollment => enrollment.Plan)
            .FirstOrDefaultAsync(charge =>
                charge.EnrollmentId == enrollmentId &&
                charge.Enrollment.Student.User.GymId == gymId &&
                charge.Enrollment.Plan.GymId == gymId);
    }

    public async Task<List<Charge>> GetByGymAsync(Guid gymId)
    {
        return await _context.Charges
            .AsNoTracking()
            .Include(charge => charge.Enrollment)
                .ThenInclude(enrollment => enrollment.Student)
                    .ThenInclude(student => student.User)
            .Include(charge => charge.Enrollment)
                .ThenInclude(enrollment => enrollment.Plan)
            .Where(charge =>
                charge.Enrollment.Student.User.GymId == gymId &&
                charge.Enrollment.Plan.GymId == gymId)
            .OrderBy(charge => charge.DueDate)
            .ThenBy(charge => charge.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(Charge charge)
    {
        _context.Charges.Update(charge);
        await _context.SaveChangesAsync();
    }
}