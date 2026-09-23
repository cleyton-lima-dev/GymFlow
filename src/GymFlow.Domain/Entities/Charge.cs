using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class Charge
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    public ChargeStatus Status { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Enrollment Enrollment { get; set; } = null!;
}