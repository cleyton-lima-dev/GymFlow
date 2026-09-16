using GymFlow.Application.DTOs.Plans;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class PlanService
{
    private readonly IPlanRepository _planRepository;

    public PlanService(IPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task<bool> CreateAsync(
        Guid gymId,
        CreatePlanRequest request)
    {
        if (!IsValid(request.Name, request.Price, request.DurationMonths, request.BillingCycle))
            return false;

        var normalizedName = request.Name.Trim();

        PersistenceTextPolicy.ValidateMaxLength(
            normalizedName,
            PersistenceTextPolicy.PlanNameMaxLength,
            "O nome do plano");

        var existingPlan = await _planRepository
            .GetByNameAsync(normalizedName, gymId);

        if (existingPlan is not null)
            return false;

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = normalizedName,
            Price = request.Price,
            DurationMonths = request.DurationMonths,
            BillingCycle = request.BillingCycle,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _planRepository.AddAsync(plan);

        return true;
    }

    public async Task<List<PlanResponse>> ListAsync(
        Guid gymId,
        bool? isActive = null)
    {
        var plans = await _planRepository
            .GetByGymAsync(gymId, isActive);

        return plans
            .Select(ToResponse)
            .ToList();
    }

    public async Task<bool> UpdateAsync(
        Guid gymId,
        Guid planId,
        UpdatePlanRequest request)
    {
        if (!IsValid(request.Name, request.Price, request.DurationMonths, request.BillingCycle))
            return false;

        var plan = await _planRepository
            .GetByIdAsync(planId, gymId);

        if (plan is null)
            return false;

        var normalizedName = request.Name.Trim();

        PersistenceTextPolicy.ValidateMaxLength(
            normalizedName,
            PersistenceTextPolicy.PlanNameMaxLength,
            "O nome do plano");

        var planWithSameName = await _planRepository
            .GetByNameAsync(normalizedName, gymId);

        if (planWithSameName is not null &&
            planWithSameName.Id != planId)
        {
            return false;
        }

        plan.Name = normalizedName;
        plan.Price = request.Price;
        plan.DurationMonths = request.DurationMonths;
        plan.BillingCycle = request.BillingCycle;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);

        return true;
    }

    public async Task<bool> UpdateStatusAsync(
        Guid gymId,
        Guid planId,
        bool isActive)
    {
        var plan = await _planRepository
            .GetByIdAsync(planId, gymId);

        if (plan is null)
            return false;

        plan.IsActive = isActive;
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepository.UpdateAsync(plan);

        return true;
    }

    private static bool IsValid(
        string name,
        decimal price,
        int durationMonths,
        PlanBillingCycle billingCycle)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (price <= 0)
            return false;

        if (durationMonths <= 0)
            return false;

        if (!Enum.IsDefined(typeof(PlanBillingCycle), billingCycle))
            return false;

        return true;
    }

    private static PlanResponse ToResponse(Plan plan)
    {
        return new PlanResponse
        {
            Id = plan.Id,
            Name = plan.Name,
            Price = plan.Price,
            DurationMonths = plan.DurationMonths,
            BillingCycle = plan.BillingCycle,
            IsActive = plan.IsActive,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
    }
}