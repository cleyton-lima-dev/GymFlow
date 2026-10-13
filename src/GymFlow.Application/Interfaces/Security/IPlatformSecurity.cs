using GymFlow.Application.DTOs.Platform;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Security;

public interface IPlatformSecurity
{
    string ProtectSecret(string secret);
    long? ValidateTotp(string protectedSecret, string code, DateTimeOffset now, long lastStep);
    PlatformLoginResponse IssueToken(PlatformOperator actor, DateTimeOffset now);
}
