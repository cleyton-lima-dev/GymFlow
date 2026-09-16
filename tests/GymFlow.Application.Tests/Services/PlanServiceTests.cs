using GymFlow.Application.DTOs.Plans;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class PlanServiceTests
{
    private readonly IPlanRepository _planRepository;
    private readonly PlanService _service;

    public PlanServiceTests()
    {
        _planRepository = Substitute.For<IPlanRepository>();
        _service = new PlanService(_planRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreatePlan()
    {
        var gymId = Guid.NewGuid();

        var request = new CreatePlanRequest
        {
            Name = "  Mensal  ",
            Price = 100,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly
        };

        _planRepository
            .GetByNameAsync("Mensal", gymId)
            .Returns((Plan?)null);

        var result = await _service.CreateAsync(gymId, request);

        Assert.True(result);

        await _planRepository.Received(1).AddAsync(
            Arg.Is<Plan>(plan =>
                plan.GymId == gymId &&
                plan.Name == "Mensal" &&
                plan.Price == 100 &&
                plan.DurationMonths == 1 &&
                plan.BillingCycle == PlanBillingCycle.Monthly &&
                plan.IsActive));
    }

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExists_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();

        _planRepository
            .GetByNameAsync("Mensal", gymId)
            .Returns(new Plan
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Mensal"
            });

        var result = await _service.CreateAsync(
            gymId,
            new CreatePlanRequest
            {
                Name = "Mensal",
                Price = 100,
                DurationMonths = 1,
                BillingCycle = PlanBillingCycle.Monthly
            });

        Assert.False(result);

        await _planRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Plan>());
    }

    [Fact]
    public async Task ListAsync_ShouldUseAuthenticatedGym()
    {
        var gymId = Guid.NewGuid();

        _planRepository
            .GetByGymAsync(gymId, true)
            .Returns(new List<Plan>());

        await _service.ListAsync(gymId, true);

        await _planRepository
            .Received(1)
            .GetByGymAsync(gymId, true);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdatePlan()
    {
        var gymId = Guid.NewGuid();

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Mensal",
            Price = 100,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly,
            IsActive = true
        };

        _planRepository
            .GetByIdAsync(plan.Id, gymId)
            .Returns(plan);

        _planRepository
            .GetByNameAsync("Mensal Atualizado", gymId)
            .Returns((Plan?)null);

        var result = await _service.UpdateAsync(
            gymId,
            plan.Id,
            new UpdatePlanRequest
            {
                Name = "Mensal Atualizado",
                Price = 110,
                DurationMonths = 1,
                BillingCycle = PlanBillingCycle.Monthly
            });

        Assert.True(result);
        Assert.Equal("Mensal Atualizado", plan.Name);
        Assert.Equal(110, plan.Price);
        Assert.NotNull(plan.UpdatedAt);

        await _planRepository
            .Received(1)
            .UpdateAsync(plan);
    }

    [Fact]
    public async Task UpdateAsync_WhenPlanDoesNotBelongToGym_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        _planRepository
            .GetByIdAsync(planId, gymId)
            .Returns((Plan?)null);

        var result = await _service.UpdateAsync(
            gymId,
            planId,
            new UpdatePlanRequest
            {
                Name = "Mensal",
                Price = 100,
                DurationMonths = 1,
                BillingCycle = PlanBillingCycle.Monthly
            });

        Assert.False(result);

        await _planRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Plan>());
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenPlanExists_ShouldUpdateStatus()
    {
        var gymId = Guid.NewGuid();

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Mensal",
            IsActive = true
        };

        _planRepository
            .GetByIdAsync(plan.Id, gymId)
            .Returns(plan);

        var result = await _service.UpdateStatusAsync(
            gymId,
            plan.Id,
            false);

        Assert.True(result);
        Assert.False(plan.IsActive);
        Assert.NotNull(plan.UpdatedAt);

        await _planRepository
            .Received(1)
            .UpdateAsync(plan);
    }

    [Fact]
    public async Task CreateAsync_WhenNameExceedsLimit_ShouldThrow()
    {
        var request = new CreatePlanRequest
        {
            Name = new string('A', 151),
            Price = 100,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));
    }
}