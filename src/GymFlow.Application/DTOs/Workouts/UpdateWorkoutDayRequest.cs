namespace GymFlow.Application.DTOs.Workouts;

public class UpdateWorkoutDayRequest
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int Order { get; set; }

    public List<UpdateWorkoutExerciseRequest> Exercises { get; set; } = new();
}