namespace GymFlow.Application.DTOs.Workouts;

public class CreateWorkoutFromTemplateRequest
{
    public Guid StudentId { get; set; }

    public Guid TemplateId { get; set; }
}