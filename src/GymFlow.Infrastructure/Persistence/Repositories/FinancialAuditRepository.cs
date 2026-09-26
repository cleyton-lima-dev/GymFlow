using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class FinancialAuditRepository : IFinancialAuditRepository
{
    private readonly AppDbContext _context;

    public FinancialAuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task StageAsync(FinancialAuditLog auditLog)
    {
        await _context.FinancialAuditLogs.AddAsync(auditLog);
    }
}