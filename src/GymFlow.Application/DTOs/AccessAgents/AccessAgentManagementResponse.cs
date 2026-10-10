namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentManagementResponse
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool ReleaseEnabled { get; set; }

    public long ConfigurationVersion { get; set; }

    public long? AppliedConfigurationVersion { get; set; }

    public bool ConfigurationApplied { get; set; }

    public int? PendingOfflineEvents { get; set; }

    public IReadOnlyList<AccessAgentDeviceStatusDto> Devices { get; set; } =
        [];

    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
