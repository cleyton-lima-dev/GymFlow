namespace GymFlow.Application.DTOs.Students;

public class CreateStudentRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }
}