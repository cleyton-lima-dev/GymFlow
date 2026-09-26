using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class FinancialAuditLog
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid ActorUserId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public FinancialAuditAction Action { get; set; }

    public string PreviousValues { get; set; } = string.Empty;

    public string NewValues { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public User ActorUser { get; set; } = null!;
}