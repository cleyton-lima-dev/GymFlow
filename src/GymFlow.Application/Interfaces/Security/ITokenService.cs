using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Security;

public interface ITokenService
{
    string GenerateToken(User user);
}