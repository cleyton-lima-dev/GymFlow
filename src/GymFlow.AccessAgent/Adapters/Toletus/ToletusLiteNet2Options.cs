namespace GymFlow.AccessAgent.Adapters.Toletus;

public sealed class ToletusLiteNet2Options
{
    public const string SectionName =
        "Devices:ToletusLiteNet2";

    public int ReconnectDelaySeconds { get; set; } = 5;

    // Keep automatic check-in confirmation off until the configured
    // LiteNet2 firmware has been validated with a real physical passage.
    public bool EnablePhysicalPassageConfirmation { get; set; }
}
