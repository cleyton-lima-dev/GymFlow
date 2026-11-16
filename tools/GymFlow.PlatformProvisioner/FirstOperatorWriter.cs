using System.Data;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.PlatformProvisioner;

public static class FirstOperatorWriter
{
    public static async Task<bool> TryCreateAsync(AppDbContext db, PlatformOperator actor,
        CancellationToken cancellationToken = default)
    {
        // Keep the first-operator check and insert in the same transaction.
        // Concurrent copies of this tool serialize on the transaction-scoped
        // lock; PostgreSQL SSI also rejects a stale predicate-read snapshot.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(131, 1)", cancellationToken);
        if (await db.PlatformOperators.AnyAsync(cancellationToken)) return false;
        db.PlatformOperators.Add(actor);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
