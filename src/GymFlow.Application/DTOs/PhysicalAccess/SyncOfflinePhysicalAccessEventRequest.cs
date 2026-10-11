using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class SyncOfflinePhysicalAccessEventRequest
{
    public Guid RequestId { get; set; }

    public string ProviderKey { get; set; } =
        string.Empty;

    public PhysicalAccessCredentialType CredentialType
    {
        get;
        set;
    }

    public string ExternalIdentifier { get; set; } =
        string.Empty;

    public DateTime OccurredAt { get; set; }

    public PhysicalAccessDecision Decision { get; set; }

    public PhysicalAccessDecisionReason Reason { get; set; }

    public PhysicalAccessDecisionSource Source { get; set; }

    public DateTime ProcessedAt { get; set; }
}