namespace GymFlow.Application.DTOs.AccessAgents;

public class UpdateAccessAgentDevicesRequest
{
    public List<AccessAgentDeviceConfigurationDto> Devices { get; set; } = [];
}