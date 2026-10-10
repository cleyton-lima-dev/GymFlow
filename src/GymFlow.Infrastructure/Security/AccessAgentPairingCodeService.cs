using System.Security.Cryptography;
using System.Text;
using GymFlow.Application.Interfaces.Security;

namespace GymFlow.Infrastructure.Security;

public class AccessAgentPairingCodeService :
    IAccessAgentPairingCodeService
{
    private const string Alphabet =
        "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private const int CodeLength = 8;

    public string GenerateCode()
    {
        var characters =
            new char[CodeLength];

        for (var index = 0;
             index < characters.Length;
             index++)
        {
            characters[index] =
                Alphabet[
                    RandomNumberGenerator
                        .GetInt32(
                            Alphabet.Length)];
        }

        var rawCode =
            new string(characters);

        return string.Concat(
            rawCode.AsSpan(0, 4),
            "-",
            rawCode.AsSpan(4, 4));
    }

    public string Hash(
        string code)
    {
        var normalized =
            Normalize(code);

        var bytes =
            Encoding.UTF8.GetBytes(
                normalized);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToBase64String(
            hash);
    }

    private static string Normalize(
        string code)
    {
        return new string(
            code
                .Where(character =>
                    !char.IsWhiteSpace(character) &&
                    character != '-')
                .Select(
                    char.ToUpperInvariant)
                .ToArray());
    }
}
