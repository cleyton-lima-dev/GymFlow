namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentDeviceConfigurationDto
{
    public string DeviceKey { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string? Host { get; set; }

    public int Port { get; set; }
}