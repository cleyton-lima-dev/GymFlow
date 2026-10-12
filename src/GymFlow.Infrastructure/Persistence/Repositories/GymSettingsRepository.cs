using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace GymFlow.Infrastructure.Persistence.Repositories;

public sealed class GymSettingsRepository(AppDbContext context) : IGymSettingsRepository
{
    // Request/job scope only: no shared mutable cache or cross-request staleness.
    private readonly Dictionary<Guid, GymSettings?> _snapshot = [];
    public GymSettings? Get(Guid gymId)
    {
        if (!_snapshot.TryGetValue(gymId, out var settings))
            _snapshot[gymId] = settings = context.GymSettings.AsNoTracking().SingleOrDefault(s => s.GymId == gymId);
        return settings;
    }
    public async Task<GymSettings?> GetAsync(Guid gymId)
    {
        if (!_snapshot.TryGetValue(gymId, out var settings))
            _snapshot[gymId] = settings = await context.GymSettings.AsNoTracking().SingleOrDefaultAsync(s => s.GymId == gymId);
        return settings;
    }
    public async Task SaveAsync(GymSettings settings, long? expectedVersion, GymSettingsAuditLog audit)
    {
        if (expectedVersion is null) context.GymSettings.Add(settings);
        else
        {
            context.GymSettings.Attach(settings);
            context.Entry(settings).State = EntityState.Modified;
            context.Entry(settings).Property(x => x.Version).OriginalValue = expectedVersion.Value;
            context.Entry(settings).Property(x => x.CreatedAt).IsModified = false;
        }
        context.GymSettingsAuditLogs.Add(audit);
        try
        {
            // EF's single SaveChanges transaction covers both settings and audit.
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (expectedVersion is null)
        {
            // PK is the atomic first-write guard. This read only classifies the
            // failed insert; it never retries it or resolves stale writes.
            if (await context.GymSettings.AsNoTracking().AnyAsync(s => s.GymId == settings.GymId))
                throw new DbUpdateConcurrencyException("Conflito ao criar configurações.", ex);
            throw;
        }
    }
}
