namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentHeartbeatResponse
{
    public bool ReleaseEnabled { get; set; }

    public long ConfigurationVersion { get; set; }

    public List<AccessAgentDeviceConfigurationDto> DesiredDevices { get; set; } = [];

    public DateTime ServerTimeUtc { get; set; }
}
