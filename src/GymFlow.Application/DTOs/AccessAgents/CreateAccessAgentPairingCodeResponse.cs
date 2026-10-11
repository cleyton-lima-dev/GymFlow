namespace GymFlow.Application.DTOs.AccessAgents;

public class CreateAccessAgentPairingCodeResponse
{
    public string Code { get; set; } =
        string.Empty;

    public DateTime ExpiresAt { get; set; }
}
