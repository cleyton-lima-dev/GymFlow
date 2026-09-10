namespace GymFlow.Domain.Entities;

public class WorkoutExerciseCompletion
{
    public Guid Id { get; set; }

    public Guid WorkoutDayProgressId { get; set; }

    public Guid WorkoutExerciseId { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public WorkoutDayProgress WorkoutDayProgress { get; set; } = null!;

    public WorkoutExercise WorkoutExercise { get; set; } = null!;
}