using GymFlow.Application.DTOs.Platform;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPlatformRepository
{
    Task<PlatformOperator?> FindOperatorAsync(string email);
    Task<PlatformOperator?> GetOperatorAsync(Guid id);
    Task<bool> HasOperatorAsync();
    Task SaveOperatorAsync(PlatformOperator actor, bool create = false);
    Task<GymOnboarding?> FindRequestAsync(Guid operatorId, string key);
    Task<GymOnboarding?> GetOnboardingAsync(Guid gymId);
    Task<User?> GetAdminAsync(Guid id, Guid gymId);
    Task<bool> EmailExistsAsync(string email);
    Task CreateAsync(GymOnboarding onboarding, GymSettings settings, User admin,
        AdminActivationInvitation invitation, GymOnboardingAuditLog audit);
    Task<AdminActivationInvitation?> FindInvitationAsync(string hash);
    Task SaveActivationAsync(GymOnboarding onboarding, User admin,
        AdminActivationInvitation invitation, GymOnboardingAuditLog audit);
    Task SaveStepAsync(GymOnboarding onboarding, GymOnboardingAuditLog audit);
    Task ReplaceInvitationAsync(GymOnboarding onboarding, AdminActivationInvitation invitation,
        GymOnboardingAuditLog audit, DateTime now);
    Task<OnboardingPage> ListAsync(int page, int pageSize);
    Task<OnboardingDetail?> DetailAsync(Guid gymId);
}
