using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IAccessAgentAuditRepository
{
    Task StageAsync(
        AccessAgentAuditLog auditLog);
}
