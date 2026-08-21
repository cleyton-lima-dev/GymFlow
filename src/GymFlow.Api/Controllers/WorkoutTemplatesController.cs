using GymFlow.Application.DTOs.WorkoutTemplates;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/workout-templates")]
[Authorize(Roles = "Admin,Professor")]
public class WorkoutTemplatesController : ControllerBase
{
    private readonly WorkoutTemplateService _workoutTemplateService;

    public WorkoutTemplatesController(
        WorkoutTemplateService workoutTemplateService)
    {
        _workoutTemplateService = workoutTemplateService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateWorkoutTemplateRequest request)
    {
        var gymId = GetGymId();

        if (gymId is null)
            return Unauthorized();

        try
        {
            var result = await _workoutTemplateService
                .CreateAsync(gymId.Value, request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
    [FromQuery] string? search,
    [FromQuery] bool? isActive,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        var gymId = GetGymId();

        if (gymId is null)
            return Unauthorized();

        try
        {
            var result = await _workoutTemplateService
                .GetAllAsync(
                    gymId.Value,
                    search,
                    isActive,
                    page,
                    pageSize);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var gymId = GetGymId();

        if (gymId is null)
            return Unauthorized();

        var result = await _workoutTemplateService
            .GetByIdAsync(id, gymId.Value);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateWorkoutTemplateRequest request)
    {
        var gymId = GetGymId();

        if (gymId is null)
            return Unauthorized();

        try
        {
            var updated = await _workoutTemplateService
                .UpdateAsync(id, gymId.Value, request);

            if (!updated)
                return NotFound();

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(
        Guid id,
        UpdateWorkoutTemplateStatusRequest request)
    {
        var gymId = GetGymId();

        if (gymId is null)
            return Unauthorized();

        var updated = await _workoutTemplateService
            .SetActiveStatusAsync(
                id,
                gymId.Value,
                request.IsActive);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    private Guid? GetGymId()
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        if (!Guid.TryParse(gymIdClaim, out var gymId))
            return null;

        return gymId;
    }
}