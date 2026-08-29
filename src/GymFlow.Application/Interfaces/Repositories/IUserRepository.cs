using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);

    Task<bool> IsActiveAsync(
        Guid userId,
        Guid gymId);

    Task AddAsync(User user);
}