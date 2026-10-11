using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class AccessAgentAuditLog
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid ActorUserId { get; set; }

    public Guid AccessAgentId { get; set; }

    public AccessAgentAuditAction Action { get; set; }

    public string PreviousValues { get; set; } = string.Empty;

    public string NewValues { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
