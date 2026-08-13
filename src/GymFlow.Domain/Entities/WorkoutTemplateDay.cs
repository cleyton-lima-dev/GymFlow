namespace GymFlow.Domain.Entities;

public class WorkoutTemplateDay
{
    public Guid Id { get; set; }

    public Guid WorkoutTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public WorkoutTemplate WorkoutTemplate { get; set; } = null!;

    public ICollection<WorkoutTemplateExercise> Exercises { get; set; } =
        new List<WorkoutTemplateExercise>();
}