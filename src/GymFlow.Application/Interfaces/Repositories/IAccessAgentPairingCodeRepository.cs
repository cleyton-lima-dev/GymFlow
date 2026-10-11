using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IAccessAgentPairingCodeRepository
{
    Task ReplaceActiveAsync(
        AccessAgentPairingCode pairingCode,
        DateTime now);

    Task<AccessAgentPairingCode?>
        TryConsumeAsync(
            string codeHash,
            DateTime now);
}
