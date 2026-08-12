namespace GymFlow.Application.DTOs.Exercises;

public class CreateExerciseRequest
{
    public string Name { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string? Description { get; set; }
}