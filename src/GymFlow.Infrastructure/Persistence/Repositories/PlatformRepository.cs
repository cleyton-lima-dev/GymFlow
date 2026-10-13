using GymFlow.Application.DTOs.Platform;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public sealed class PlatformRepository(AppDbContext context) : IPlatformRepository
{
    public Task<PlatformOperator?> FindOperatorAsync(string email) => context.PlatformOperators.SingleOrDefaultAsync(x => x.Email == email);
    public Task<PlatformOperator?> GetOperatorAsync(Guid id) => context.PlatformOperators.SingleOrDefaultAsync(x => x.Id == id);
    public Task<bool> HasOperatorAsync() => context.PlatformOperators.AnyAsync();
    public async Task SaveOperatorAsync(PlatformOperator actor, bool create = false)
    {
        if (create) context.PlatformOperators.Add(actor);
        await CommitAsync();
    }
    public Task<GymOnboarding?> FindRequestAsync(Guid actor, string key) => context.GymOnboardings.AsNoTracking()
        .SingleOrDefaultAsync(x => x.OperatorId == actor && x.IdempotencyKey == key);
    public Task<GymOnboarding?> GetOnboardingAsync(Guid gym) => context.GymOnboardings.SingleOrDefaultAsync(x => x.GymId == gym);
    public Task<User?> GetAdminAsync(Guid id, Guid gym) => context.Users.SingleOrDefaultAsync(x => x.Id == id && x.GymId == gym);
    public Task<bool> EmailExistsAsync(string email) => context.Users.AnyAsync(x => x.Email == email);
    public async Task CreateAsync(GymOnboarding onboarding, GymSettings settings, User admin,
        AdminActivationInvitation invitation, GymOnboardingAuditLog audit)
    {
        context.AddRange(onboarding, settings, admin, invitation, audit);
        // A single SaveChanges uses EF's implicit transaction for every row and audit.
        await CommitAsync();
    }
    public Task<AdminActivationInvitation?> FindInvitationAsync(string hash) => context.AdminActivationInvitations
        .SingleOrDefaultAsync(x => x.TokenHash == hash);
    public async Task SaveActivationAsync(GymOnboarding onboarding, User admin,
        AdminActivationInvitation invitation, GymOnboardingAuditLog audit)
    {
        context.GymOnboardingAuditLogs.Add(audit);
        await CommitAsync();
    }
    public async Task SaveStepAsync(GymOnboarding onboarding, GymOnboardingAuditLog audit)
    {
        context.GymOnboardingAuditLogs.Add(audit);
        await CommitAsync();
    }
    public async Task ReplaceInvitationAsync(GymOnboarding onboarding, AdminActivationInvitation invitation,
        GymOnboardingAuditLog audit, DateTime now)
    {
        var previous = await context.AdminActivationInvitations.Where(x => x.GymId == onboarding.GymId &&
            x.ConsumedAt == null && x.RevokedAt == null).ToListAsync();
        foreach (var item in previous) item.RevokedAt = now;
        context.AddRange(invitation, audit);
        await CommitAsync();
    }
    private IQueryable<OnboardingSummary> Summaries(Guid? gym = null, int skip = 0, int take = 100)
    {
        var academies = context.GymOnboardings.AsNoTracking().Where(x => gym == null || x.GymId == gym)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.GymId).Skip(skip).Take(take);
        return from o in academies
        join s in context.GymSettings.AsNoTracking() on o.GymId equals s.GymId
        join u in context.Users.AsNoTracking() on new { Id = o.AdminId, o.GymId } equals new { u.Id, u.GymId }
        select new OnboardingSummary(o.GymId, s.AdministrativeName, s.TimeZoneId, u.Name, u.Email,
            o.CompletedAt != null ? "completed" : o.AdminActivatedAt == null ? "awaiting-admin" :
            !o.ConfigurationConfirmed ? "configuration" : o.PhysicalAccessContracted && o.PhysicalValidationEvidence == null
            ? "physical-validation" : "ready", o.PhysicalAccessContracted, o.ConfigurationConfirmed,
            o.PhysicalValidationEvidence, o.AdminActivatedAt, o.CompletedAt, o.Version, o.CreatedAt);
    }
    public async Task<OnboardingPage> ListAsync(int page, int pageSize)
    {
        var count = await context.GymOnboardings.CountAsync();
        var rows = await Summaries(skip: (page - 1) * pageSize, take: pageSize).ToListAsync();
        return new(rows, count, page, pageSize);
    }
    public async Task<OnboardingDetail?> DetailAsync(Guid gym)
    {
        var row = await Summaries(gym).SingleOrDefaultAsync();
        if (row is null) return null;
        var audit = await context.GymOnboardingAuditLogs.AsNoTracking().Where(x => x.GymId == gym)
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).Take(100)
            .Select(x => new OnboardingAuditResponse(x.ActorType, x.Action, x.Changes, x.OccurredAt)).ToListAsync();
        return new(row, audit);
    }
    private async Task CommitAsync()
    {
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { context.ChangeTracker.Clear(); throw new PlatformConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { context.ChangeTracker.Clear(); throw new PlatformConflictException(); }
    }
}
