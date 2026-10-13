namespace GymFlow.Domain.Entities;

public sealed class PlatformOperator
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ProtectedTotpSecret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long CredentialVersion { get; set; } = 1;
    public long LastTotpStep { get; set; } = -1;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}
