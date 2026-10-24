using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class PhysicalAccessEvent
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid RequestId { get; set; }

    // Historical events remain null: their originating agent/device cannot be
    // reconstructed safely from the event payload.
    public Guid? AccessAgentId { get; set; }

    public string? DeviceKey { get; set; }

    // Nullable for historical rows that predate strict request idempotency.
    // This is a one-way digest and never stores the presented credential.
    public string? RequestFingerprint { get; set; }

    public Guid? StudentId { get; set; }

    public Guid? CredentialId { get; set; }

    public PhysicalAccessDecision Decision { get; set; }

    public PhysicalAccessDecisionReason Reason { get; set; }

    public PhysicalAccessDecisionSource Source { get; set; } =
    PhysicalAccessDecisionSource.Online;

    public DateTime OccurredAt { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public Student? Student { get; set; }

    public PhysicalAccessCredential? Credential { get; set; }
}
