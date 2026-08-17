namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutDayResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public List<WorkoutExerciseResponse> Exercises { get; set; } = new();
}