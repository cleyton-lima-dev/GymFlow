namespace GymFlow.Application.DTOs.WorkoutTemplates;

public class UpdateWorkoutTemplateDayRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int Order { get; set; }

    public List<UpdateWorkoutTemplateExerciseRequest> Exercises { get; set; } = new();
}
