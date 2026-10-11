using GymFlow.Application.Interfaces.Security;
using System.Security.Cryptography;
using System.Text;

namespace GymFlow.Infrastructure.Security;

public class AccessAgentSecretService : IAccessAgentSecretService
{
    private const int SecretSizeInBytes = 32;

    public string GenerateSecret()
    {
        var bytes =
            RandomNumberGenerator.GetBytes(
                SecretSizeInBytes);

        return Convert.ToBase64String(bytes);
    }

    public string Hash(string secret)
    {
        var secretBytes =
            Encoding.UTF8.GetBytes(secret);

        var hashBytes =
            SHA256.HashData(secretBytes);

        return Convert.ToBase64String(hashBytes);
    }

    public bool Verify(
        string secret,
        string secretHash)
    {
        var providedHash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(secret));

        byte[] storedHash;

        try
        {
            storedHash =
                Convert.FromBase64String(secretHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            providedHash,
            storedHash);
    }
}