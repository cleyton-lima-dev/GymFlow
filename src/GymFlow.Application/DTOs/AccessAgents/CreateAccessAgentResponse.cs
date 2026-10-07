namespace GymFlow.Application.DTOs.AccessAgents;

public class CreateAccessAgentResponse
{
    public Guid AgentId { get; set; }

    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}