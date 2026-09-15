namespace GymFlow.Domain.Entities;

public class WorkoutDay
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int Order { get; set; }

    public Workout Workout { get; set; } = null!;

    public ICollection<WorkoutExercise> Exercises { get; set; } =
        new List<WorkoutExercise>();

    public ICollection<WorkoutExecution> Executions { get; set; } =
        new List<WorkoutExecution>();
}