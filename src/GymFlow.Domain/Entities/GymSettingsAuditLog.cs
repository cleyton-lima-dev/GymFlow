namespace GymFlow.Domain.Entities;

public sealed class GymSettingsAuditLog
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public Guid ActorUserId { get; set; }
    public string PreviousValues { get; set; } = string.Empty;
    public string NewValues { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
