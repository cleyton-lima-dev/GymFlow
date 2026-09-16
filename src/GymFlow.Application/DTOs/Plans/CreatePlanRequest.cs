using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Plans;

public class CreatePlanRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public PlanBillingCycle BillingCycle { get; set; }
}