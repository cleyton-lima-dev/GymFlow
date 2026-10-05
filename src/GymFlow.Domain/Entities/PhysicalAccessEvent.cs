using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class PhysicalAccessEvent
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid RequestId { get; set; }

    public Guid? StudentId { get; set; }

    public Guid? CredentialId { get; set; }

    public PhysicalAccessDecision Decision { get; set; }

    public PhysicalAccessDecisionReason Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public Student? Student { get; set; }

    public PhysicalAccessCredential? Credential { get; set; }
}