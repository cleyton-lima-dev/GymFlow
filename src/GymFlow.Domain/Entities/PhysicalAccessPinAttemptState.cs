namespace GymFlow.Domain.Entities;

public sealed class PhysicalAccessPinAttemptState
{
    public Guid GymId { get; set; }
    public long WindowStart { get; set; }
    public int Failures { get; set; }
    public int Pending { get; set; }
    public long LockedUntil { get; set; }
}
