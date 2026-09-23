using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Charges;

public class ChargeResponse
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    public ChargeStatus Status { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}