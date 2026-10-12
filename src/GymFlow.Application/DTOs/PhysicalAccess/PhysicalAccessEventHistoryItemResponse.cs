using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class PhysicalAccessEventHistoryItemResponse
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid? StudentId { get; set; }

    public string? StudentName { get; set; }

    public PhysicalAccessDecision Decision { get; set; }

    public PhysicalAccessDecisionReason Reason { get; set; }

    public PhysicalAccessDecisionSource Source { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime ProcessedAt { get; set; }
}
