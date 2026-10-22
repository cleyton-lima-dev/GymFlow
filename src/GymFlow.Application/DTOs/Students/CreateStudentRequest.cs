namespace GymFlow.Application.DTOs.Students;

public class CreateStudentRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }
}
