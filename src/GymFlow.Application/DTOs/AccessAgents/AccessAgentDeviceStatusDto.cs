namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentDeviceStatusDto
{
    public string DeviceKey { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public bool Connected { get; set; }

    public string? Endpoint { get; set; }

    public DateTime? LastCredentialReadAt { get; set; }

    public int? LastCredentialType { get; set; }
}