using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GymFlow.Application.Security;

// RFC 6238 / RFC 4226: 30-second steps, HMAC-SHA1, six digits.
public static class PlatformTotp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    public static string NewSecret() => Encode(RandomNumberGenerator.GetBytes(20));
    public static string Generate(string secret, long step, int digits = 6)
    {
        if (digits is not (6 or 8) || step < 0) throw new ArgumentException("Parâmetros TOTP inválidos.");
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var bytes = Decode(secret);
        try
        {
            var hash = HMACSHA1.HashData(bytes, counter);
            var offset = hash[^1] & 15;
            var value = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF;
            return (value % (digits == 6 ? 1000000u : 100000000u)).ToString(new string('0', digits), CultureInfo.InvariantCulture);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static long? Validate(string secret, string code, DateTimeOffset now, long lastStep)
    {
        if (code.Length != 6 || code.Any(c => c < '0' || c > '9')) return null;
        var current = now.ToUnixTimeSeconds() / 30;
        foreach (var step in new[] { current, current - 1, current + 1 })
            if (step >= 0 && step > lastStep && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(Generate(secret, step)))) return step;
        return null;
    }
    private static byte[] Decode(string value)
    {
        var output = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var c in value)
        {
            var part = Alphabet.IndexOf(c);
            if (part < 0) throw new ArgumentException("Segredo TOTP inválido.");
            buffer = (buffer << 5) | part; bits += 5;
            if (bits >= 8) { bits -= 8; output.Add((byte)(buffer >> bits)); }
        }
        return output.ToArray();
    }
    private static string Encode(byte[] value)
    {
        var output = new StringBuilder(); var buffer = 0; var bits = 0;
        foreach (var part in value)
        {
            buffer = (buffer << 8) | part; bits += 8;
            while (bits >= 5) { bits -= 5; output.Append(Alphabet[(buffer >> bits) & 31]); }
        }
        if (bits > 0) output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }
}
