using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Plans;

public class PlanResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public PlanBillingCycle BillingCycle { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}