using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class AccessAgentPairingCodeRepository :
    IAccessAgentPairingCodeRepository
{
    private readonly AppDbContext _context;

    public AccessAgentPairingCodeRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task ReplaceActiveAsync(
        AccessAgentPairingCode pairingCode,
        DateTime now)
    {
        await _context
            .AccessAgentPairingCodes
            .Where(existing =>
                existing.GymId ==
                    pairingCode.GymId &&
                existing.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        existing =>
                            existing.ConsumedAt,
                        now));

        await _context
            .AccessAgentPairingCodes
            .AddAsync(pairingCode);

        await _context.SaveChangesAsync();
    }

    public async Task<AccessAgentPairingCode?>
        TryConsumeAsync(
            string codeHash,
            DateTime now)
    {
        var affectedRows =
            await _context
                .AccessAgentPairingCodes
                .Where(pairingCode =>
                    pairingCode.CodeHash ==
                        codeHash &&
                    pairingCode.ConsumedAt ==
                        null &&
                    pairingCode.ExpiresAt >
                        now)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            pairingCode =>
                                pairingCode.ConsumedAt,
                            now));

        if (affectedRows != 1)
        {
            return null;
        }

        return await _context
            .AccessAgentPairingCodes
            .AsNoTracking()
            .SingleAsync(
                pairingCode =>
                    pairingCode.CodeHash ==
                        codeHash);
    }
}
