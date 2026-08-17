namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutResponse
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid? SourceWorkoutTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}