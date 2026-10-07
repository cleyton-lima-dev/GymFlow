using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Security;

public interface IAccessAgentTokenService
{
    string GenerateToken(AccessAgent agent);
}