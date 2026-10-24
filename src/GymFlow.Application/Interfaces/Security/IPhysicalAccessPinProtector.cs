namespace GymFlow.Application.Interfaces.Security;

public interface IPhysicalAccessPinProtector
{
    string ComputeLookupHash(Guid gymId, string pin);
}
