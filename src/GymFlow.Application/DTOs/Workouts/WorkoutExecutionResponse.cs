namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutExecutionResponse
{
    public Guid Id { get; set; }

    public Guid WorkoutDayId { get; set; }

    public DateTime CompletedAt { get; set; }
}
