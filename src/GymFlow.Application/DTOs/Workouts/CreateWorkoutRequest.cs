namespace GymFlow.Application.DTOs.Workouts;

public class CreateWorkoutRequest
{
    public Guid StudentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<CreateWorkoutDayRequest> Days { get; set; } = new();
}