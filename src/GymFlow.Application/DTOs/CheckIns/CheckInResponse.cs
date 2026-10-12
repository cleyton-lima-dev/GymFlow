using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.CheckIns;

public class CheckInResponse
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } =
        string.Empty;

    public Guid RequestId { get; set; }

    public CheckInSource Source { get; set; }

    public Guid? PhysicalAccessEventId { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}