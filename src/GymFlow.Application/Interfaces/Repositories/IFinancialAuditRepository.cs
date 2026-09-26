using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IFinancialAuditRepository
{
    Task StageAsync(FinancialAuditLog auditLog);
}