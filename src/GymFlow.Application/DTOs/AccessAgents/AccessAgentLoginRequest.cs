namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentLoginRequest
{
    public Guid AgentId { get; set; }

    public Guid GymId { get; set; }

    public string Secret { get; set; } = string.Empty;
}