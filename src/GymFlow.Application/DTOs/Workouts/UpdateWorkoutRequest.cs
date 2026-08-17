namespace GymFlow.Application.DTOs.Workouts;

public class UpdateWorkoutRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<UpdateWorkoutDayRequest> Days { get; set; } = new();
}