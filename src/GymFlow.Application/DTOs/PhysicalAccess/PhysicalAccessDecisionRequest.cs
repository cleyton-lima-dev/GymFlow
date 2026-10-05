using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class PhysicalAccessDecisionRequest
{
    public Guid RequestId { get; set; }

    public string ProviderKey { get; set; } = string.Empty;

    public PhysicalAccessCredentialType CredentialType { get; set; }

    public string ExternalIdentifier { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}