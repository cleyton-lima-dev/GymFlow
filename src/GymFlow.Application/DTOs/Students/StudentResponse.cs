namespace GymFlow.Application.DTOs.Students;

public class StudentResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}