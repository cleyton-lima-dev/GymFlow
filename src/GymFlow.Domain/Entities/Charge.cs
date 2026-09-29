using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class Charge
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; } = 0m;

    public decimal? PaidAmount { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    public DateOnly DueDate { get; set; }

    public ChargeStatus Status { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Enrollment Enrollment { get; set; } = null!;
}