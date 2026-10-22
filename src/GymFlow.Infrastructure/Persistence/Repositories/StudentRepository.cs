using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using GymFlow.Application.DTOs.Students;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.Logging;
using GymFlow.Application.Services;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<StudentRepository>? _logger;

    public StudentRepository(AppDbContext context, ILogger<StudentRepository>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(Student student)
    {
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
            .Where(student => student.GymId == gymId)
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
                    student.Name,
                    $"%{searchTerm}%") ||
                (student.Email != null &&
                    EF.Functions.ILike(
                        student.Email,
                        $"%{searchTerm}%")) ||
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
                student.IsActive == isActive.Value);
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
            .OrderBy(student => student.Name)
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
                student.GymId == gymId);
    }

    public async Task<List<Guid>> GetGymIdsForLifecycleAsync()
    {
        return await _context.Students
            .AsNoTracking()
            .Where(student => student.ArchivedAt == null)
            .Select(student => student.GymId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<Student>> GetByGymIdForLifecycleAsync(
    Guid gymId)
    {
        return await _context.Students
            .Include(student => student.User)
            .Where(student =>
                student.GymId == gymId &&
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

    public async Task<bool> TrySaveLifecycleChangesAsync()
    {
        try
        {
            // A stale administrative or financial state defers the lifecycle batch.
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Never retry stale entities; the next scheduled pass reads fresh state.
            _context.ChangeTracker.Clear();
            _logger?.LogWarning("Student lifecycle batch deferred because activity or inactivation reason changed concurrently.");
            return false;
        }
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
                x.GymId == gymId);
    }

    public Task<Student?> GetByInviteHashAsync(string tokenHash) =>
        _context.Students.SingleOrDefaultAsync(x => x.AppInviteTokenHash == tokenHash);

    public async Task AddUserAndSaveAsync(User user, Student student)
    {
        await _context.Users.AddAsync(user);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _context.Users.AsNoTracking().AnyAsync(x => x.Email == user.Email))
                throw new StudentIdentityConflictException();
            throw;
        }
    }
}
