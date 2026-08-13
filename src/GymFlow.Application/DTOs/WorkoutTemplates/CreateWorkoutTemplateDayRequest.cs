namespace GymFlow.Application.DTOs.WorkoutTemplates;

public class CreateWorkoutTemplateDayRequest
{
    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public List<CreateWorkoutTemplateExerciseRequest> Exercises { get; set; } = new();
}