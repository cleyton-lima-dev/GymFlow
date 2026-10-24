namespace GymFlow.Application.Interfaces.Security;

public sealed record PhysicalAccessPinReservation(Guid GymId, long WindowStart);

public interface IPhysicalAccessPinAttemptLimiter
{
    Task<PhysicalAccessPinReservation?> TryReserveAsync(Guid gymId);
    Task CompleteAsync(PhysicalAccessPinReservation reservation, bool failed);
}
