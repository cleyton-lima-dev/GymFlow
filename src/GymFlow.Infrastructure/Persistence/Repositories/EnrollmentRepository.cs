using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class EnrollmentRepository : IEnrollmentRepository
{
    private readonly AppDbContext _context;

    public EnrollmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Enrollment enrollment)
    {
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();
    }

    public async Task<Enrollment?> GetByIdAsync(
        Guid enrollmentId,
        Guid gymId)
    {
        return await _context.Enrollments
            .Include(enrollment => enrollment.Student)
                .ThenInclude(student => student.User)
            .Include(enrollment => enrollment.Plan)
            .Include(enrollment => enrollment.Charge)
            .FirstOrDefaultAsync(enrollment =>
                enrollment.Id == enrollmentId &&
                enrollment.Student.User.GymId == gymId &&
                enrollment.Plan.GymId == gymId);
    }

    public async Task<Enrollment?> GetActiveByStudentAsync(
    Guid studentId,
    Guid gymId,
    DateOnly referenceDate)
    {
        return await _context.Enrollments
            .Include(enrollment => enrollment.Student)
                .ThenInclude(student => student.User)
            .FirstOrDefaultAsync(enrollment =>
                enrollment.StudentId == studentId &&
                enrollment.Student.User.GymId == gymId &&
                enrollment.Status == EnrollmentStatus.Active &&
                enrollment.StartDate <= referenceDate &&
                enrollment.EndDate > referenceDate);
    }

    public async Task<List<Enrollment>> GetByGymAsync(
        Guid gymId,
        EnrollmentStatus? status)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Include(enrollment => enrollment.Student)
                .ThenInclude(student => student.User)
            .Include(enrollment => enrollment.Plan)
            .Where(enrollment =>
                enrollment.Student.User.GymId == gymId &&
                enrollment.Plan.GymId == gymId);

        if (status.HasValue)
        {
            query = query.Where(enrollment =>
                enrollment.Status == status.Value);
        }

        return await query
            .OrderBy(enrollment => enrollment.EndDate)
            .ThenBy(enrollment => enrollment.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(Enrollment enrollment)
    {
        _context.Enrollments.Update(enrollment);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasOverlappingEnrollmentAsync(
    Guid studentId,
    Guid gymId,
    DateOnly startDate,
    DateOnly endDate)
    {
        return await _context.Enrollments
            .AnyAsync(enrollment =>
                enrollment.StudentId == studentId &&
                enrollment.Student.User.GymId == gymId &&
                enrollment.Plan.GymId == gymId &&
                enrollment.Status == EnrollmentStatus.Active &&
                startDate < enrollment.EndDate &&
                endDate > enrollment.StartDate);
    }

    public async Task<List<Enrollment>> GetByStudentIdsAsync(
    IReadOnlyCollection<Guid> studentIds,
    Guid gymId)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        return await _context.Enrollments
            .AsNoTracking()
            .Include(enrollment => enrollment.Student)
                .ThenInclude(student => student.User)
            .Where(enrollment =>
                studentIds.Contains(enrollment.StudentId) &&
                enrollment.Student.User.GymId == gymId &&
                enrollment.Plan.GymId == gymId)
            .OrderByDescending(enrollment => enrollment.StartDate)
            .ThenByDescending(enrollment => enrollment.CreatedAt)
            .ToListAsync();
    }
}