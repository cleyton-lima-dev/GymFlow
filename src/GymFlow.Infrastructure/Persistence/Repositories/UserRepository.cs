using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using GymFlow.Domain.Enums;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<bool> IsActiveAsync(
    Guid userId,
    Guid gymId)
    {
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(user =>
                user.Id == userId &&
                user.GymId == gymId &&
                user.IsActive);
    }

    public async Task<IReadOnlyList<User>> GetProfessorsByGymIdAsync(
    Guid gymId)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.GymId == gymId &&
                user.Role == UserRole.Professor)
            .OrderBy(user => user.Name)
            .ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }
}
