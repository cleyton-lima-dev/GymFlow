using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GymFlow.AccessAgent.Security;

public class WindowsAgentCredentialStore : IAgentCredentialStore
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes(
            "Avelri.AccessAgent.Credentials.v1");

    private readonly string _filePath;

    public WindowsAgentCredentialStore()
    {
        var baseDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);

        var directory =
            Path.Combine(
                baseDirectory,
                "Avelri",
                "AccessAgent");

        _filePath =
            Path.Combine(
                directory,
                "credentials.dat");
    }

    public async Task SaveAsync(
        AgentCredentials credentials,
        CancellationToken cancellationToken)
    {
        EnsureWindows();

        var directory =
            Path.GetDirectoryName(_filePath)!;

        Directory.CreateDirectory(directory);

        var json =
            JsonSerializer.Serialize(credentials);

        var plainBytes =
            Encoding.UTF8.GetBytes(json);

        var protectedBytes =
            ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.LocalMachine);

        await File.WriteAllBytesAsync(
            _filePath,
            protectedBytes,
            cancellationToken);
    }

    public async Task<AgentCredentials?> LoadAsync(
        CancellationToken cancellationToken)
    {
        EnsureWindows();

        if (!File.Exists(_filePath))
            return null;

        var protectedBytes =
            await File.ReadAllBytesAsync(
                _filePath,
                cancellationToken);

        var plainBytes =
            ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.LocalMachine);

        var json =
            Encoding.UTF8.GetString(plainBytes);

        return JsonSerializer.Deserialize<AgentCredentials>(
            json);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "O armazenamento seguro do Avelri Access Agent requer Windows.");
        }
    }
}