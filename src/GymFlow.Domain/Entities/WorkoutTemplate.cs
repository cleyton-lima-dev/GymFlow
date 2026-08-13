namespace GymFlow.Domain.Entities;

public class WorkoutTemplate
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<WorkoutTemplateDay> Days { get; set; } =
        new List<WorkoutTemplateDay>();
}