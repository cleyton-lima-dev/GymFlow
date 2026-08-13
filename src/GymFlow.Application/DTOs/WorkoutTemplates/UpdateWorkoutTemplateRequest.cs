namespace GymFlow.Application.DTOs.WorkoutTemplates;

public class UpdateWorkoutTemplateRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<UpdateWorkoutTemplateDayRequest> Days { get; set; } = new();
}