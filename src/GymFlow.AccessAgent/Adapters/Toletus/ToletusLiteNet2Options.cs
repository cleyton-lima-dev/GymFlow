namespace GymFlow.AccessAgent.Adapters.Toletus;

public sealed class ToletusLiteNet2Options
{
    public const string SectionName =
        "Devices:ToletusLiteNet2";

    public int ReconnectDelaySeconds { get; set; } = 5;
}