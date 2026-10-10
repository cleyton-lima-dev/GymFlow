namespace GymFlow.AccessAgent.Adapters.Toletus;

public sealed class ToletusLiteNet2Options
{
    public const string SectionName =
        "Devices:ToletusLiteNet2";

    public string DeviceKey { get; set; } =
        "primary";

    public bool Enabled { get; set; }

    public string Host { get; set; } =
        string.Empty;

    public int Port { get; set; } = 7878;

    public int ReconnectDelaySeconds { get; set; } = 5;
}
