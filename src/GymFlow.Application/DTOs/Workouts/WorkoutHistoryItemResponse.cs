namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutHistoryItemResponse
{
    public Guid ExecutionId { get; set; }

    public Guid WorkoutId { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public Guid WorkoutDayId { get; set; }

    public string WorkoutDayName { get; set; } = string.Empty;

    public DateTime CompletedAt { get; set; }
}