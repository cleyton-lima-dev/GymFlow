namespace GymFlow.Domain.Entities;

public sealed class GymMaintenanceAuditLog
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public Guid ActorPlatformOperatorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? AllowedAdminUserId { get; set; }
    public DateTime OccurredAt { get; set; }
}
