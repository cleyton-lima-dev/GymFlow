namespace GymFlow.Application.DTOs.Exercises;

public class UpdateExerciseRequest
{
    public string Name { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string? Description { get; set; }
}