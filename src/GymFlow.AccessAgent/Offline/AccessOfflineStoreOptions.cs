namespace GymFlow.AccessAgent.Offline;

public sealed class AccessOfflineStoreOptions
{
    public const string SectionName =
        "OfflineStore";

    public string DatabasePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "Avelri",
            "AccessAgent",
            "access-agent.db");

    public int PermissionCacheMinutes { get; set; } = 30;

    public int SyncBatchSize { get; set; } = 100;
}