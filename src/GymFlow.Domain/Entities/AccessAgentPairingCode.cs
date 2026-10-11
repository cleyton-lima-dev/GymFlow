namespace GymFlow.Domain.Entities;

public class AccessAgentPairingCode
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CodeHash { get; set; } =
        string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;
}
