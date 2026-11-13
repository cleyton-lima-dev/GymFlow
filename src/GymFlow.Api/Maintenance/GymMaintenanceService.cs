using System.Data;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Api.Maintenance;

public sealed class GymMaintenanceService(AppDbContext db, TimeProvider clock)
{
    public Task<GymMaintenanceState?> GetAsync(Guid gymId, CancellationToken token = default) =>
        db.GymMaintenanceStates.AsNoTracking().SingleOrDefaultAsync(x => x.GymId == gymId, token);

    public async Task<bool> IsAllowedAsync(Guid gymId, Guid userId, CancellationToken token = default)
    {
        var state = await GetAsync(gymId, token);
        if (state is null || !state.IsActive) return true;
        if (state.AllowedAdminUserId != userId) return false;
        return await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.GymId == gymId &&
            x.IsActive && x.Role == UserRole.Admin, token);
    }

    public async Task<GymMaintenanceState> ChangeAsync(Guid gymId, Guid actorId, Guid? adminId,
        bool activate, CancellationToken token = default)
    {
        if (gymId == Guid.Empty || actorId == Guid.Empty || (activate && adminId.GetValueOrDefault() == Guid.Empty))
            throw new ArgumentException("Identidade inválida.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var actorValid = await db.PlatformOperators.AsNoTracking().AnyAsync(x => x.Id == actorId && x.IsActive, token);
        if (!actorValid) throw new UnauthorizedAccessException();
        if (activate && !await db.Users.AsNoTracking().AnyAsync(x => x.Id == adminId && x.GymId == gymId &&
            x.IsActive && x.Role == UserRole.Admin, token))
            throw new ArgumentException("Admin autorizado inválido para a academia.");

        var state = await db.GymMaintenanceStates.SingleOrDefaultAsync(x => x.GymId == gymId, token);
        if (state?.IsActive == activate) throw new GymMaintenanceConflictException();
        var now = clock.GetUtcNow().UtcDateTime;
        if (state is null)
        {
            state = new GymMaintenanceState { GymId = gymId, IsActive = true,
                AllowedAdminUserId = adminId, Version = 1, UpdatedAt = now };
            db.GymMaintenanceStates.Add(state);
        }
        else
        {
            state.IsActive = activate;
            state.AllowedAdminUserId = activate ? adminId : null;
            state.Version = checked(state.Version + 1);
            state.UpdatedAt = now;
        }
        db.GymMaintenanceAuditLogs.Add(new GymMaintenanceAuditLog
        {
            Id = Guid.NewGuid(), GymId = gymId, ActorPlatformOperatorId = actorId,
            Action = activate ? "activate" : "deactivate", AllowedAdminUserId = activate ? adminId : null,
            OccurredAt = now
        });
        try
        {
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (DbUpdateConcurrencyException) { throw new GymMaintenanceConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" or "40001" })
        { throw new GymMaintenanceConflictException(); }
        catch (PostgresException ex) when (ex.SqlState is "23505" or "40001")
        { throw new GymMaintenanceConflictException(); }
        return state;
    }
}

public sealed class GymMaintenanceConflictException : Exception;
