using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class CreatePhysicalAccessCredentialRequest
{
    public Guid StudentId { get; set; }

    public PhysicalAccessCredentialType Type { get; set; }

    public string ProviderKey { get; set; } = string.Empty;

    public string ExternalIdentifier { get; set; } = string.Empty;
}