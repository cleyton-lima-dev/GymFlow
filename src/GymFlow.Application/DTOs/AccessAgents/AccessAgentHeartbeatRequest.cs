namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentHeartbeatRequest
{
    public long? AppliedConfigurationVersion { get; set; }

    public int PendingOfflineEvents { get; set; }

    public List<AccessAgentDeviceStatusDto> Devices { get; set; } = [];
}
