namespace GymFlow.Domain.Entities;

public class Workout
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid GymId { get; set; }

    public Guid? SourceWorkoutTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;

    public ICollection<WorkoutDay> Days { get; set; } =
        new List<WorkoutDay>();

}