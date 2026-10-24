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

    internal WindowsAgentCredentialStore(string filePath)
    {
        _filePath = filePath;
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

        var temporaryPath =
            Path.Combine(
                directory,
                $"credentials.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                await stream.WriteAsync(
                    protectedBytes,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);
            }

            if (File.Exists(_filePath))
                File.Replace(temporaryPath, _filePath, null);
            else
                File.Move(temporaryPath, _filePath);
        }
        finally
        {
            // Only a temporary file created by this write may be removed.
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
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
