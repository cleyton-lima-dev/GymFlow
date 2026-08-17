namespace GymFlow.Application.DTOs.Workouts;

public class CreateWorkoutExerciseRequest
{
    public Guid ExerciseId { get; set; }

    public int Sets { get; set; }

    public string Repetitions { get; set; } = string.Empty;

    public int? RestSeconds { get; set; }

    public string? Notes { get; set; }

    public int Order { get; set; }
}