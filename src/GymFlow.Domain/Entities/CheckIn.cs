using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class CheckIn
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid StudentId { get; set; }

    public Guid RequestId { get; set; }

    public CheckInSource Source { get; set; }

    public Guid? PhysicalAccessEventId { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public Student Student { get; set; } = null!;

    public PhysicalAccessEvent? PhysicalAccessEvent
    {
        get;
        set;
    }
}