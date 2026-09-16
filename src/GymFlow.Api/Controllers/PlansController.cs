using GymFlow.Application.DTOs.Plans;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize(Roles = "Admin")]
public class PlansController : ControllerBase
{
    private readonly PlanService _planService;

    public PlansController(PlanService planService)
    {
        _planService = planService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePlanRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var created = await _planService.CreateAsync(
                gymId,
                request);

            if (!created)
            {
                return Conflict(new
                {
                    message = "Não foi possível cadastrar o plano."
                });
            }

            return StatusCode(201, new
            {
                message = "Plano criado com sucesso."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var plans = await _planService.ListAsync(
            gymId,
            isActive);

        return Ok(plans);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdatePlanRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var updated = await _planService.UpdateAsync(
                gymId,
                id,
                request);

            if (!updated)
            {
                return Conflict(new
                {
                    message = "Não foi possível atualizar o plano."
                });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdatePlanStatusRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var updated = await _planService.UpdateStatusAsync(
            gymId,
            id,
            request.IsActive);

        if (!updated)
        {
            return NotFound(new
            {
                message = "Plano não encontrado."
            });
        }

        return NoContent();
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(gymIdClaim, out gymId);
    }
}