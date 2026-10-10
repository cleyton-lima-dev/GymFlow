namespace GymFlow.Domain.Entities;

public class AccessAgent
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool ReleaseEnabled { get; set; }

    public long ConfigurationVersion { get; set; } = 1;

    public long? AppliedConfigurationVersion { get; set; }

    public int? PendingOfflineEvents { get; set; }

    public string? DeviceStatusesJson { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
