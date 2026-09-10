namespace GymFlow.Application.DTOs.Workouts;

public class WorkoutExerciseCompletionResponse
{
    public Guid WorkoutDayId { get; set; }

    public Guid WorkoutExerciseId { get; set; }

    public bool IsCompleted { get; set; }

    public int CompletedExercises { get; set; }

    public int TotalExercises { get; set; }
}