namespace GymFlow.Domain.Entities;

public class WorkoutExecution
{
    public Guid Id { get; set; }

    public Guid WorkoutDayId { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public WorkoutDay WorkoutDay { get; set; } = null!;
}