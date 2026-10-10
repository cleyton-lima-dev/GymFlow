namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentHeartbeatRequest
{
    public long? AppliedConfigurationVersion { get; set; }

    public int PendingOfflineEvents { get; set; }

    public List<AccessAgentDeviceStatusDto> Devices { get; set; } = [];

    public DateTime? LastOfflineSyncAt { get; set; }

    public DateTime? LastFailureAt { get; set; }

    public string? LastFailureCode { get; set; }
}
