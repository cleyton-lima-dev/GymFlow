namespace GymFlow.Domain.Entities;

public class WorkoutTemplateExercise
{
    public Guid Id { get; set; }

    public Guid WorkoutTemplateDayId { get; set; }

    public Guid ExerciseId { get; set; }

    public int Sets { get; set; }

    public string Repetitions { get; set; } = string.Empty;

    public int? RestSeconds { get; set; }

    public string? Notes { get; set; }

    public int Order { get; set; }

    public WorkoutTemplateDay WorkoutTemplateDay { get; set; } = null!;

    public Exercise Exercise { get; set; } = null!;
}