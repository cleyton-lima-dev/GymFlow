namespace GymFlow.Domain.Entities;

public class Student
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}