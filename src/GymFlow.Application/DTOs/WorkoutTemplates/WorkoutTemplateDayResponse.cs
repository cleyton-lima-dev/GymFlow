namespace GymFlow.Application.DTOs.WorkoutTemplates;

public class WorkoutTemplateDayResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public List<WorkoutTemplateExerciseResponse> Exercises { get; set; } = new();
}