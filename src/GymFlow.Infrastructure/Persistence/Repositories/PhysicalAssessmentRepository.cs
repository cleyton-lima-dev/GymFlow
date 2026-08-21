using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class PhysicalAssessmentRepository
    : IPhysicalAssessmentRepository
{
    private readonly AppDbContext _context;

    public PhysicalAssessmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PhysicalAssessment assessment)
    {
        await _context.PhysicalAssessments.AddAsync(assessment);
    }

    public async Task<PhysicalAssessment?> GetByIdAsync(
        Guid assessmentId,
        Guid studentId,
        Guid gymId)
    {
        return await _context.PhysicalAssessments
            .AsNoTracking()
            .Include(x => x.Student)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.Id == assessmentId &&
                x.StudentId == studentId &&
                x.Student.User.GymId == gymId);
    }

    public async Task<PhysicalAssessment?> GetLatestByStudentAsync(
        Guid studentId,
        Guid gymId)
    {
        return await _context.PhysicalAssessments
            .AsNoTracking()
            .Include(x => x.Student)
                .ThenInclude(x => x.User)
            .Where(x =>
                x.StudentId == studentId &&
                x.Student.User.GymId == gymId)
            .OrderByDescending(x => x.AssessmentDate)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PhysicalAssessment>> GetHistoryByStudentAsync(
        Guid studentId,
        Guid gymId,
        int page,
        int pageSize)
    {
        return await _context.PhysicalAssessments
            .AsNoTracking()
            .Include(x => x.Student)
                .ThenInclude(x => x.User)
            .Where(x =>
                x.StudentId == studentId &&
                x.Student.User.GymId == gymId)
            .OrderByDescending(x => x.AssessmentDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByStudentAsync(
        Guid studentId,
        Guid gymId)
    {
        return await _context.PhysicalAssessments
            .AsNoTracking()
            .CountAsync(x =>
                x.StudentId == studentId &&
                x.Student.User.GymId == gymId);
    }

    public async Task<bool> ExistsForDateAsync(
        Guid studentId,
        Guid gymId,
        DateOnly assessmentDate)
    {
        return await _context.PhysicalAssessments
            .AsNoTracking()
            .AnyAsync(x =>
                x.StudentId == studentId &&
                x.AssessmentDate == assessmentDate &&
                x.Student.User.GymId == gymId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}