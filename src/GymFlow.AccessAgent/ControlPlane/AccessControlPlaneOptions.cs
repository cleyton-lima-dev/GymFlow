namespace GymFlow.AccessAgent.ControlPlane;

public class AccessControlPlaneOptions
{
    public const string SectionName =
        "ControlPlane";

    public int HeartbeatIntervalSeconds { get; set; } =
        60;
}
