using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class PhysicalAccessCredentialResponse
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public PhysicalAccessCredentialType Type { get; set; }

    public string ProviderKey { get; set; } = string.Empty;

    public string ExternalIdentifier { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}