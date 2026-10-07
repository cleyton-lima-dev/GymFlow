namespace GymFlow.Application.DTOs.AccessAgents;

public class AccessAgentLoginResponse
{
    public Guid AgentId { get; set; }

    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}