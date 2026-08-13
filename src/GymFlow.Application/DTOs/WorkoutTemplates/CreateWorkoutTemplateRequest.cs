namespace GymFlow.Application.DTOs.WorkoutTemplates;

public class CreateWorkoutTemplateRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<CreateWorkoutTemplateDayRequest> Days { get; set; } = new();
}