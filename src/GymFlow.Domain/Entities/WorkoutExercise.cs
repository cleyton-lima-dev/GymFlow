namespace GymFlow.Domain.Entities;

public class WorkoutExercise
{
    public Guid Id { get; set; }

    public Guid WorkoutDayId { get; set; }

    public Guid ExerciseId { get; set; }

    public int Sets { get; set; }

    public string Repetitions { get; set; } = string.Empty;

    public int? RestSeconds { get; set; }

    public string? Notes { get; set; }

    public int Order { get; set; }

    public WorkoutDay WorkoutDay { get; set; } = null!;

    public Exercise Exercise { get; set; } = null!;
}