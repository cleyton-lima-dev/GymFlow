namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutExerciseResponse
{
    public Guid Id { get; set; }

    public Guid ExerciseId { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public int Sets { get; set; }

    public string Repetitions { get; set; } = string.Empty;

    public int? RestSeconds { get; set; }

    public string? Notes { get; set; }

    public int Order { get; set; }
}