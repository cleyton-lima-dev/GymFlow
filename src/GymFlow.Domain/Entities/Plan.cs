using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public PlanBillingCycle BillingCycle { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}