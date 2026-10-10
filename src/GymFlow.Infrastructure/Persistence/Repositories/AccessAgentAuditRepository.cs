using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class AccessAgentAuditRepository :
    IAccessAgentAuditRepository
{
    private readonly AppDbContext _context;

    public AccessAgentAuditRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task StageAsync(
        AccessAgentAuditLog auditLog)
    {
        await _context.AccessAgentAuditLogs
            .AddAsync(auditLog);
    }
}
