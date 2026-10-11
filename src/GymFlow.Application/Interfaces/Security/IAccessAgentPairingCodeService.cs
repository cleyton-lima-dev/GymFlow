namespace GymFlow.Application.Interfaces.Security;

public interface IAccessAgentPairingCodeService
{
    string GenerateCode();

    string Hash(string code);
}
