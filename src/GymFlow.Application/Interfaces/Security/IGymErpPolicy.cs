namespace GymFlow.Application.Interfaces.Security;

public interface IGymErpPolicy
{
    bool IsEnabled(Guid gymId);
}
