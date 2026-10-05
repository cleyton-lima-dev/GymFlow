using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class PhysicalAccessCredential
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid StudentId { get; set; }

    public PhysicalAccessCredentialType Type { get; set; }

    public string ProviderKey { get; set; } = string.Empty;

    public string ExternalIdentifier { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;
}