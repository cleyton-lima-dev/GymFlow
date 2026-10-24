namespace GymFlow.AccessAgent.Security;

public interface ILocalAgentStateProbe
{
    IDisposable AcquirePairingLock();

    bool HasPotentialLocalState();
}
