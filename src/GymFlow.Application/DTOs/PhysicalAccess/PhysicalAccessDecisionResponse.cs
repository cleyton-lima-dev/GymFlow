using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class PhysicalAccessDecisionResponse
{
    public Guid RequestId { get; set; }

    public PhysicalAccessDecision Decision { get; set; }

    public PhysicalAccessDecisionReason Reason { get; set; }

    public Guid? StudentId { get; set; }

    public Guid? CredentialId { get; set; }

    public DateTime ProcessedAt { get; set; }
}