using GymFlow.AccessAgent.Offline;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Security;

public sealed class FileSystemLocalAgentStateProbe : ILocalAgentStateProbe
{
    private const string PairingLockFileName = "pairing.lock";
    private readonly string _dataDirectory;
    private readonly string _databasePath;

    public FileSystemLocalAgentStateProbe(
        IOptions<AccessOfflineStoreOptions> options)
        : this(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Avelri",
                "AccessAgent"),
            options.Value.DatabasePath)
    {
    }

    public FileSystemLocalAgentStateProbe(
        string dataDirectory,
        string databasePath)
    {
        _dataDirectory = dataDirectory;
        _databasePath = databasePath;
    }

    public bool HasPotentialLocalState()
    {
        try
        {
            if (Directory.EnumerateFileSystemEntries(_dataDirectory)
                .Any(path => !string.Equals(
                    Path.GetFileName(path),
                    PairingLockFileName,
                    StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        catch (DirectoryNotFoundException)
        {
            // An absent data directory is normal before the first pairing.
        }

        // A configured SQLite location may be outside ProgramData. Include its
        // journal files without reading or changing any database contents.
        return ExistsOrIsInaccessible(_databasePath) ||
               ExistsOrIsInaccessible(_databasePath + "-wal") ||
               ExistsOrIsInaccessible(_databasePath + "-shm");
    }

    public IDisposable AcquirePairingLock()
    {
        Directory.CreateDirectory(_dataDirectory);

        return new FileStream(
            Path.Combine(_dataDirectory, PairingLockFileName),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.DeleteOnClose);
    }

    private static bool ExistsOrIsInaccessible(string path)
    {
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
