namespace GymFlow.Application.Validation;

public static class PersistenceTextPolicy
{
    public const int UserNameMaxLength = 150;
    public const int EmailMaxLength = 200;
    public const int PhoneMaxLength = 20;

    public const int ExerciseNameMaxLength = 150;
    public const int MuscleGroupMaxLength = 100;

    public const int WorkoutNameMaxLength = 150;
    public const int DayNameMaxLength = 100;
    public const int RepetitionsMaxLength = 50;

    public const int DescriptionMaxLength = 500;
    public const int NotesMaxLength = 500;

    public static void ValidateMaxLength(
        string? value,
        int maxLength,
        string fieldName)
    {
        if (value is null)
            return;

        var valueToPersist = value.Trim();

        if (valueToPersist.Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} deve possuir no máximo {maxLength} caracteres.");
        }
    }
}