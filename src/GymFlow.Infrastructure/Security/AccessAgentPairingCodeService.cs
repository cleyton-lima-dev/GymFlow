using System.Security.Cryptography;
using System.Text;
using GymFlow.Application.Interfaces.Security;
using Microsoft.Extensions.Configuration;

namespace GymFlow.Infrastructure.Security;

public class AccessAgentPairingCodeService :
    IAccessAgentPairingCodeService
{
    private const string Alphabet =
        "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private const int CodeLength = 8;

    private const string KeyDerivationContext =
        "Avelri.AccessAgent.PairingCode.v1";

    private readonly byte[] _hmacKey;

    public AccessAgentPairingCodeService(
        IConfiguration configuration)
    {
        var jwtKey =
            configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(
                jwtKey))
        {
            throw new InvalidOperationException(
                "Jwt:Key não configurada para proteção dos códigos de pareamento.");
        }

        using var keyDerivation =
            new HMACSHA256(
                Encoding.UTF8.GetBytes(
                    jwtKey));

        _hmacKey =
            keyDerivation.ComputeHash(
                Encoding.UTF8.GetBytes(
                    KeyDerivationContext));
    }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(
            code);

        var normalized =
            Normalize(code);

        var bytes =
            Encoding.UTF8.GetBytes(
                normalized);

        using var hmac =
            new HMACSHA256(
                _hmacKey);

        var hash =
            hmac.ComputeHash(
                bytes);

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