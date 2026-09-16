using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Enrollments;

public class EnrollmentResponse
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public Guid PlanId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public decimal PlanPrice { get; set; }

    public int PlanDurationMonths { get; set; }

    public PlanBillingCycle PlanBillingCycle { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public EnrollmentStatus Status { get; set; }

    public DateOnly? CancellationDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}