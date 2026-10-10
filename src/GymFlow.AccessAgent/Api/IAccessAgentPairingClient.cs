namespace GymFlow.AccessAgent.Api;

public interface IAccessAgentPairingClient
{
    Task<PairAccessAgentResult?> PairAsync(
        string apiBaseUrl,
        string pairingCode,
        string machineName,
        CancellationToken cancellationToken);
}

public sealed record PairAccessAgentResult(
    Guid AgentId,
    Guid GymId,
    string Name,
    string MachineName,
    string Secret);
