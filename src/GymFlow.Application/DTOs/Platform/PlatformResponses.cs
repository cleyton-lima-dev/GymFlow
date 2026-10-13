namespace GymFlow.Application.DTOs.Platform;

public sealed record PlatformLoginResponse(string Token, DateTime ExpiresAt, string Name);
public sealed record OnboardingSummary(Guid GymId, string AdministrativeName, string TimeZoneId,
    string AdminName, string AdminEmail, string Status, bool PhysicalAccessContracted,
    bool ConfigurationConfirmed, string? PhysicalValidationEvidence, DateTime? AdminActivatedAt,
    DateTime? CompletedAt, long Version, DateTime CreatedAt);
public sealed record OnboardingPage(IReadOnlyList<OnboardingSummary> Items, int Total, int Page, int PageSize);
public sealed record InvitationResult(Guid GymId, long Version, string? Token, DateTime? ExpiresAt, bool Replayed = false);
public sealed record OnboardingAuditResponse(string ActorType, string Action, string Changes, DateTime OccurredAt);
public sealed record OnboardingDetail(OnboardingSummary Academy, IReadOnlyList<OnboardingAuditResponse> Audit);
