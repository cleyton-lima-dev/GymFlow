namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutDayResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int Order { get; set; }

    public List<WorkoutExerciseResponse> Exercises { get; set; } = new();

    public bool CompletedToday { get; set; }

    public DateTime? LastCompletedAt { get; set; }

    public int? LastCompletedAtUtcOffsetMinutes { get; set; }
}