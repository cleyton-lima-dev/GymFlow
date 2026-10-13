using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymFlow.Application.DTOs.Platform;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Security;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public sealed class PlatformOnboardingService(IPlatformRepository repository, IPasswordHasher passwords,
    IPlatformSecurity security, TimeProvider clock)
{
    public async Task<PlatformLoginResponse?> LoginAsync(PlatformLoginRequest request)
    {
        var actor = await repository.FindOperatorAsync(request.Email.Trim().ToLowerInvariant());
        var now = clock.GetUtcNow();
        if (actor is null || !actor.IsActive || actor.LockedUntil > now.UtcDateTime) return null;
        var step = passwords.Verify(request.Password, actor.PasswordHash)
            ? security.ValidateTotp(actor.ProtectedTotpSecret, request.Code, now, actor.LastTotpStep) : null;
        if (step is null)
        {
            actor.FailedAttempts++;
            if (actor.FailedAttempts >= 5) actor.LockedUntil = now.UtcDateTime.AddMinutes(15);
            try { await repository.SaveOperatorAsync(actor); } catch (PlatformConflictException) { }
            return null;
        }
        actor.LastTotpStep = step.Value;
        actor.FailedAttempts = 0;
        actor.LockedUntil = null;
        try { await repository.SaveOperatorAsync(actor); } catch (PlatformConflictException) { return null; }
        return security.IssueToken(actor, now);
    }

    public async Task<InvitationResult> ProvisionAsync(Guid actorId, string key, ProvisionGymRequest request)
    {
        if (!Guid.TryParse(key, out var requestId) || requestId == Guid.Empty)
            throw new ArgumentException("Informe uma chave de idempotência UUID válida.");
        key = requestId.ToString("D");
        var name = request.AdministrativeName.Trim();
        var adminName = request.AdminName.Trim();
        var email = request.AdminEmail.Trim().ToLowerInvariant();
        if (name.Length is 0 or > 150 || adminName.Length is 0 or > 150 ||
            email.Length > 200 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Verifique o nome fantasia e os dados do primeiro Admin.");
        var zone = GymTimeZonePolicy.ValidateIana(request.TimeZoneId);
        var hash = Hash(JsonSerializer.Serialize(new { name, zone, adminName, email, request.PhysicalAccessContracted }));
        var existing = await repository.FindRequestAsync(actorId, key);
        if (existing is not null) return Replay(existing, hash);
        if (await repository.EmailExistsAsync(email)) throw new PlatformConflictException();
        var now = clock.GetUtcNow().UtcDateTime;
        var gymId = Guid.NewGuid();
        var admin = new User { Id = Guid.NewGuid(), GymId = gymId, Name = adminName, Email = email,
            Role = UserRole.Admin, IsActive = false, CreatedAt = now,
            // Unrecoverable random credential; invitation activation replaces it.
            PasswordHash = passwords.Hash(NewToken()) };
        var onboarding = new GymOnboarding { GymId = gymId, OperatorId = actorId, AdminId = admin.Id,
            IdempotencyKey = key, RequestHash = hash, PhysicalAccessContracted = request.PhysicalAccessContracted,
            CreatedAt = now, UpdatedAt = now };
        var token = NewToken();
        var invite = Invitation(gymId, admin.Id, token, now);
        try
        {
            await repository.CreateAsync(onboarding, new GymSettings { GymId = gymId, AdministrativeName = name,
                TimeZoneId = zone, Version = 1, CreatedAt = now, UpdatedAt = now }, admin, invite,
                Audit(gymId, actorId, "operator", "provisioned", new { name, zone, admin.Id, request.PhysicalAccessContracted }));
        }
        catch (PlatformConflictException)
        {
            existing = await repository.FindRequestAsync(actorId, key);
            if (existing is not null) return Replay(existing, hash);
            throw;
        }
        return new(gymId, onboarding.Version, token, invite.ExpiresAt);
    }

    public async Task<bool> ActivateAsync(ActivateAdminRequest request)
    {
        PasswordPolicy.Validate(request.Password);
        var now = clock.GetUtcNow().UtcDateTime;
        var invite = await repository.FindInvitationAsync(Hash(request.Token));
        if (invite is null || invite.ConsumedAt is not null || invite.RevokedAt is not null || invite.ExpiresAt <= now) return false;
        var onboarding = await repository.GetOnboardingAsync(invite.GymId);
        if (onboarding is null || onboarding.AdminId != invite.AdminId || onboarding.AdminActivatedAt is not null) return false;
        var admin = await repository.GetAdminAsync(invite.AdminId, invite.GymId);
        if (admin is null || admin.IsActive || admin.Role != UserRole.Admin) return false;
        admin.PasswordHash = passwords.Hash(request.Password);
        admin.IsActive = true;
        admin.UpdatedAt = now;
        invite.ConsumedAt = now;
        onboarding.AdminActivatedAt = now;
        Advance(onboarding, now);
        await repository.SaveActivationAsync(onboarding, admin, invite,
            Audit(invite.GymId, admin.Id, "admin", "admin-activated", new { ActivatedAt = now }));
        return true;
    }

    public async Task<InvitationResult?> ReissueAsync(Guid actorId, Guid gymId, long version)
    {
        var onboarding = await repository.GetOnboardingAsync(gymId);
        if (onboarding is null) return null;
        if (onboarding.Version != version || onboarding.AdminActivatedAt is not null) throw new PlatformConflictException();
        var now = clock.GetUtcNow().UtcDateTime;
        var token = NewToken();
        var invite = Invitation(gymId, onboarding.AdminId, token, now);
        Advance(onboarding, now);
        await repository.ReplaceInvitationAsync(onboarding, invite,
            Audit(gymId, actorId, "operator", "invitation-reissued", new { invite.ExpiresAt }), now);
        return new(gymId, onboarding.Version, token, invite.ExpiresAt);
    }

    public async Task<bool> StepAsync(Guid actorId, Guid gymId, OnboardingStepRequest request)
    {
        var onboarding = await repository.GetOnboardingAsync(gymId);
        if (onboarding is null) return false;
        if (onboarding.Version != request.Version || onboarding.CompletedAt is not null) throw new PlatformConflictException();
        var previous = new { onboarding.ConfigurationConfirmed, onboarding.PhysicalValidationEvidence, onboarding.CompletedAt };
        var now = clock.GetUtcNow().UtcDateTime;
        switch (request.Step)
        {
            case "configuration": onboarding.ConfigurationConfirmed = true; break;
            case "physical":
                if (!onboarding.PhysicalAccessContracted || string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Trim().Length > 1000)
                    throw new ArgumentException("Registre a evidência da homologação física prevista no contrato.");
                onboarding.PhysicalValidationEvidence = request.Evidence.Trim(); break;
            case "complete":
                if (onboarding.AdminActivatedAt is null || !onboarding.ConfigurationConfirmed ||
                    (onboarding.PhysicalAccessContracted && onboarding.PhysicalValidationEvidence is null))
                    throw new ArgumentException("Conclua as etapas obrigatórias antes de encerrar a implantação.");
                onboarding.CompletedAt = now; break;
            default: throw new ArgumentException("Etapa inválida.");
        }
        Advance(onboarding, now);
        await repository.SaveStepAsync(onboarding, Audit(gymId, actorId, "operator", "step-" + request.Step,
            new { Previous = previous, Current = new { onboarding.ConfigurationConfirmed, onboarding.PhysicalValidationEvidence, onboarding.CompletedAt } }));
        return true;
    }

    private GymOnboardingAuditLog Audit(Guid gym, Guid actor, string type, string action, object changes) =>
        new() { Id = Guid.NewGuid(), GymId = gym, ActorId = actor, ActorType = type, Action = action,
            Changes = JsonSerializer.Serialize(changes), OccurredAt = clock.GetUtcNow().UtcDateTime };
    private static void Advance(GymOnboarding onboarding, DateTime now) { onboarding.Version = checked(onboarding.Version + 1); onboarding.UpdatedAt = now; }
    private static InvitationResult Replay(GymOnboarding existing, string hash) => existing.RequestHash == hash
        ? new(existing.GymId, existing.Version, null, null, true) : throw new PlatformConflictException();
    private static AdminActivationInvitation Invitation(Guid gym, Guid admin, string token, DateTime now) => new()
        { Id = Guid.NewGuid(), GymId = gym, AdminId = admin, TokenHash = Hash(token), CreatedAt = now, ExpiresAt = now.AddHours(24) };
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
