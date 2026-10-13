namespace GymFlow.Domain.Entities;

public sealed class GymOnboarding
{
    public Guid GymId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid AdminId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public bool PhysicalAccessContracted { get; set; }
    public bool ConfigurationConfirmed { get; set; }
    public string? PhysicalValidationEvidence { get; set; }
    public DateTime? AdminActivatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
