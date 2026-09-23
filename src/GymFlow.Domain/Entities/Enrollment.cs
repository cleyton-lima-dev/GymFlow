using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class Enrollment
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid PlanId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public DateOnly? CancellationDate { get; set; }

    // Snapshot das condições contratadas.
    public string PlanName { get; set; } = string.Empty;

    public decimal PlanPrice { get; set; }

    public int PlanDurationMonths { get; set; }

    public PlanBillingCycle PlanBillingCycle { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;

    public Plan Plan { get; set; } = null!;

    public Charge? Charge { get; set; }
}