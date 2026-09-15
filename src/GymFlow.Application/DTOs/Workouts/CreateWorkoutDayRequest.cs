namespace GymFlow.Application.DTOs.Workouts;

public class CreateWorkoutDayRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int Order { get; set; }

    public List<CreateWorkoutExerciseRequest> Exercises { get; set; } = new();
}
