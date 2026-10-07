namespace GymFlow.Application.Interfaces.Security;

public interface IAccessAgentSecretService
{
    string GenerateSecret();

    string Hash(string secret);

    bool Verify(
        string secret,
        string secretHash);
}