using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
    int skip,
    int take)
    {
        var query = _context.Students
            .AsNoTracking()
            .Include(student => student.User)
            .Where(student => student.User.GymId == gymId)
            .AsQueryable();

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

    public async Task UpdateAsync(Student student)
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