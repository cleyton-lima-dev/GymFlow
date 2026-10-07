using System.Security.Cryptography;
using System.Text;

namespace GymFlow.AccessAgent.Security;

public sealed class WindowsLocalSensitiveDataProtector :
    ILocalSensitiveDataProtector
{
    private static readonly byte[] DataEntropy =
        Encoding.UTF8.GetBytes(
            "Avelri.AccessAgent.LocalData.v1");

    private static readonly byte[] KeyEntropy =
        Encoding.UTF8.GetBytes(
            "Avelri.AccessAgent.LookupKey.v1");

    private readonly object _keyLock = new();

    private readonly string _keyPath;

    private byte[]? _lookupKey;

    public WindowsLocalSensitiveDataProtector()
    {
        var directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Avelri",
                "AccessAgent");

        _keyPath =
            Path.Combine(
                directory,
                "lookup.key");
    }

    public string Protect(string value)
    {
        EnsureWindows();

        ArgumentNullException.ThrowIfNull(value);

        var plainBytes =
            Encoding.UTF8.GetBytes(value);

        var protectedBytes =
            ProtectedData.Protect(
                plainBytes,
                DataEntropy,
                DataProtectionScope.LocalMachine);

        return Convert.ToBase64String(
            protectedBytes);
    }

    public string Unprotect(
        string protectedValue)
    {
        EnsureWindows();

        ArgumentNullException.ThrowIfNull(
            protectedValue);

        var protectedBytes =
            Convert.FromBase64String(
                protectedValue);

        var plainBytes =
            ProtectedData.Unprotect(
                protectedBytes,
                DataEntropy,
                DataProtectionScope.LocalMachine);

        return Encoding.UTF8.GetString(
            plainBytes);
    }

    public string ComputeLookupHash(
        string providerKey,
        int credentialType,
        string externalIdentifier)
    {
        EnsureWindows();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerKey);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            externalIdentifier);

        var lookupKey =
            GetOrCreateLookupKey();

        var material =
            BuildLookupMaterial(
                providerKey,
                credentialType,
                externalIdentifier);

        using var hmac =
            new HMACSHA256(lookupKey);

        var hash =
            hmac.ComputeHash(material);

        return Convert.ToBase64String(hash);
    }

    private byte[] GetOrCreateLookupKey()
    {
        if (_lookupKey is not null)
            return _lookupKey;

        lock (_keyLock)
        {
            if (_lookupKey is not null)
                return _lookupKey;

            var directory =
                Path.GetDirectoryName(_keyPath)!;

            Directory.CreateDirectory(directory);

            if (File.Exists(_keyPath))
            {
                var protectedKey =
                    File.ReadAllBytes(_keyPath);

                _lookupKey =
                    ProtectedData.Unprotect(
                        protectedKey,
                        KeyEntropy,
                        DataProtectionScope.LocalMachine);

                return _lookupKey;
            }

            var key =
                RandomNumberGenerator.GetBytes(32);

            var protectedKeyBytes =
                ProtectedData.Protect(
                    key,
                    KeyEntropy,
                    DataProtectionScope.LocalMachine);

            File.WriteAllBytes(
                _keyPath,
                protectedKeyBytes);

            _lookupKey = key;

            return _lookupKey;
        }
    }

    private static byte[] BuildLookupMaterial(
        string providerKey,
        int credentialType,
        string externalIdentifier)
    {
        using var stream =
            new MemoryStream();

        using var writer =
            new BinaryWriter(
                stream,
                Encoding.UTF8,
                leaveOpen: true);

        writer.Write(providerKey);
        writer.Write(credentialType);
        writer.Write(externalIdentifier);
        writer.Flush();

        return stream.ToArray();
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "A proteção local do Avelri Access Agent requer Windows.");
        }
    }
}