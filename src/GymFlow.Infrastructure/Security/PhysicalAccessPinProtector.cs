using System.Security.Cryptography;
using System.Text;
using GymFlow.Application.Interfaces.Security;
using Microsoft.Extensions.Configuration;

namespace GymFlow.Infrastructure.Security;

public sealed class PhysicalAccessPinProtector : IPhysicalAccessPinProtector
{
    private readonly IConfiguration _configuration;

    public PhysicalAccessPinProtector(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ComputeLookupHash(Guid gymId, string pin)
    {
        if (gymId == Guid.Empty || pin.Length != 4 ||
            !pin.All(character => character is >= '0' and <= '9'))
        {
            throw new ArgumentException("O PIN deve conter exatamente quatro dígitos.");
        }

        var configuredKey = _configuration["PhysicalAccess:PinLookupKey"];
        byte[] key;
        try
        {
            key = Convert.FromBase64String(configuredKey ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("A chave de busca de PIN não está configurada.");
        }

        if (key.Length < 32)
        {
            throw new InvalidOperationException("A chave de busca de PIN não está configurada.");
        }

        var material = Encoding.UTF8.GetBytes($"avelri-pin-v1:{gymId:N}:{pin}");
        return "pin:v1:" + Convert.ToHexString(HMACSHA256.HashData(key, material));
    }
}
