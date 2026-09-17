using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using GymFlow.Application.DTOs.Students;
using GymFlow.Domain.Enums;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public StudentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user, Student student)
    {
        await _context.Users.AddAsync(user);
        await _context.Students.AddAsync(student);

        await _context.SaveChangesAsync();
    }

    public async Task<(List<Student> Items, int TotalCount)> GetPagedByGymIdAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    StudentEnrollmentFilter? enrollmentFilter,
    StudentArchiveFilter archiveFilter,
    DateOnly referenceDate,
    int skip,
    int take)
    {
        var query = _context.Students
            .AsNoTracking()
            .Include(student => student.User)
            .Where(student => student.User.GymId == gymId)
            .AsQueryable();
        query = archiveFilter switch
        {
            StudentArchiveFilter.NotArchived =>
                query.Where(student =>
                    student.ArchivedAt == null),

            StudentArchiveFilter.Archived =>
                query.Where(student =>
                    student.ArchivedAt != null),

            StudentArchiveFilter.All =>
                query,

            _ => throw new ArgumentOutOfRangeException(
                nameof(archiveFilter))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(student =>
                EF.Functions.ILike(
                    student.User.Name,
                    $"%{searchTerm}%") ||
                EF.Functions.ILike(
                    student.User.Email,
                    $"%{searchTerm}%") ||
                (
                    student.Phone != null &&
                    EF.Functions.ILike(
                        student.Phone,
                        $"%{searchTerm}%")
                ));
        }

        if (isActive.HasValue)
        {
            query = query.Where(student =>
                student.User.IsActive == isActive.Value);
        }
        if (enrollmentFilter.HasValue)
        {
            if (enrollmentFilter == StudentEnrollmentFilter.None)
            {
                query = query.Where(student =>
                    !_context.Enrollments.Any(enrollment =>
                        enrollment.StudentId == student.Id &&
                        enrollment.Plan.GymId == gymId));
            }
            else
            {
                var desiredStatus = enrollmentFilter.Value switch
                {
                    StudentEnrollmentFilter.Active =>
                        EnrollmentStatus.Active,

                    StudentEnrollmentFilter.Cancelled =>
                        EnrollmentStatus.Cancelled,

                    StudentEnrollmentFilter.Expired =>
                        EnrollmentStatus.Expired,

                    _ => throw new ArgumentOutOfRangeException()
                };

                query = query.Where(student =>
                    _context.Enrollments
                        .Where(enrollment =>
                            enrollment.StudentId == student.Id &&
                            enrollment.Plan.GymId == gymId)
                        .OrderByDescending(enrollment =>
                            enrollment.Status ==
                                EnrollmentStatus.Active &&
                            enrollment.StartDate <= referenceDate &&
                            enrollment.EndDate > referenceDate)
                        .ThenByDescending(enrollment =>
                            enrollment.StartDate)
                        .ThenByDescending(enrollment =>
                            enrollment.CreatedAt)
                        .Select(enrollment =>
                            enrollment.Status ==
                                EnrollmentStatus.Active &&
                            enrollment.EndDate <= referenceDate
                                ? EnrollmentStatus.Expired
                                : enrollment.Status)
                        .FirstOrDefault() == desiredStatus);
            }
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(student => student.User.Name)
            .ThenBy(student => student.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Student?> GetByIdAndGymIdAsync(Guid studentId, Guid gymId)
    {
        return await _context.Students
            .Include(student => student.User)
            .FirstOrDefaultAsync(student =>
                student.Id == studentId &&
                student.User.GymId == gymId);
    }

    public async Task<List<Guid>> GetGymIdsForLifecycleAsync()
    {
        return await _context.Students
            .AsNoTracking()
            .Where(student => student.ArchivedAt == null)
            .Select(student => student.User.GymId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<Student>> GetByGymIdForLifecycleAsync(
    Guid gymId)
    {
        return await _context.Students
            .Include(student => student.User)
            .Where(student =>
                student.User.GymId == gymId &&
                student.ArchivedAt == null)
            .ToListAsync();
    }

    public async Task UpdateAsync(Student student)
    {
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<Student?> GetByUserIdAndGymIdAsync(
    Guid userId,
    Guid gymId)
    {
        return await _context.Students
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.User.GymId == gymId);
    }
}