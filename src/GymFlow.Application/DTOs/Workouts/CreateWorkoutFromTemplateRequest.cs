namespace GymFlow.Application.DTOs.Workouts;

public class CreateWorkoutFromTemplateRequest
{
    public Guid StudentId { get; set; }

    public Guid TemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<CreateWorkoutDayRequest> Days { get; set; } = [];
}