namespace GymFlow.Domain.Entities;

public class WorkoutDayProgress
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid WorkoutDayId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Student Student { get; set; } = null!;

    public WorkoutDay WorkoutDay { get; set; } = null!;

    public ICollection<WorkoutExerciseCompletion> CompletedExercises { get; set; } =
        new List<WorkoutExerciseCompletion>();
}