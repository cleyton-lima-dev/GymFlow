using System.Security.Claims;
using GymFlow.Application.DTOs.Workouts;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/workouts")]
[Authorize]
public class WorkoutsController : ControllerBase
{
    private readonly WorkoutService _workoutService;

    public WorkoutsController(
        WorkoutService workoutService)
    {
        _workoutService = workoutService;
    }

    [Authorize(Roles = "Admin,Professor")]
    [HttpPost]
    public async Task<IActionResult> CreateManual(
        CreateWorkoutRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _workoutService
                .CreateManualAsync(gymId, request);

            return CreatedAtAction(
                nameof(GetCurrentByStudent),
                new { studentId = result.StudentId },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize(Roles = "Admin,Professor")]
    [HttpPost("from-template")]
    public async Task<IActionResult> CreateFromTemplate(
        CreateWorkoutFromTemplateRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _workoutService
                .CreateFromTemplateAsync(
                    gymId,
                    request);

            return CreatedAtAction(
                nameof(GetCurrentByStudent),
                new { studentId = result.StudentId },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize(Roles = "Admin,Professor")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateWorkoutRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _workoutService
                .UpdateAsync(
                    gymId,
                    id,
                    request);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize(Roles = "Admin,Professor")]
    [HttpGet("students/{studentId:guid}/current")]
    public async Task<IActionResult> GetCurrentByStudent(
        Guid studentId)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _workoutService
                .GetActiveByStudentAsync(
                    gymId,
                    studentId);

            if (result is null)
            {
                return NotFound(new
                {
                    message =
                        "O aluno não possui treino ativo."
                });
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize(Roles = "Student")]
    [HttpGet("me/current")]
    public async Task<IActionResult> GetMyCurrent()
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _workoutService
                .GetActiveForUserAsync(
                    gymId,
                    userId);

            if (result is null)
            {
                return NotFound(new
                {
                    message =
                        "Você não possui treino ativo."
                });
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize(Roles = "Student")]
    [HttpPost("me/days/{workoutDayId:guid}/complete")]
    public async Task<IActionResult> CompleteMyDay(
        Guid workoutDayId)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _workoutService
                .CompleteDayForUserAsync(
                    gymId,
                    userId,
                    workoutDayId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize(Roles = "Student")]
    [HttpGet("me/history")]
    public async Task<IActionResult> GetMyHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _workoutService
                .GetHistoryForUserAsync(
                    gymId,
                    userId,
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

    private bool TryGetGymId(
        out Guid gymId)
    {
        var gymIdClaim =
            User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(
            gymIdClaim,
            out gymId);
    }

    private bool TryGetUserId(
        out Guid userId)
    {
        var userIdClaim = User
            .FindFirst(
                ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            userIdClaim,
            out userId);
    }
}