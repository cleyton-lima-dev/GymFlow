namespace GymFlow.Domain.Entities;

public sealed class AdminActivationInvitation
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public Guid AdminId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
