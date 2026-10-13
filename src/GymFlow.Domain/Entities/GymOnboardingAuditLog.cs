namespace GymFlow.Domain.Entities;

public sealed class GymOnboardingAuditLog
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
