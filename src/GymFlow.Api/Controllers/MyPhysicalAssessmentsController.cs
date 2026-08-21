using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/physical-assessments/me")]
[Authorize(Roles = "Student")]
public class MyPhysicalAssessmentsController : ControllerBase
{
    private readonly PhysicalAssessmentService _physicalAssessmentService;

    public MyPhysicalAssessmentsController(
        PhysicalAssessmentService physicalAssessmentService)
    {
        _physicalAssessmentService = physicalAssessmentService;
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var assessment = await _physicalAssessmentService
            .GetLatestForUserAsync(userId, gymId);

        if (assessment is null)
        {
            return NotFound(new
            {
                message = "Nenhuma avaliação física encontrada."
            });
        }

        return Ok(assessment);
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
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
            var history = await _physicalAssessmentService
                .GetHistoryForUserAsync(
                    userId,
                    gymId,
                    page,
                    pageSize);

            return Ok(history);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
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

    [HttpGet("{assessmentId:guid}")]
    public async Task<IActionResult> GetById(
        Guid assessmentId)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var assessment = await _physicalAssessmentService
            .GetByIdForUserAsync(
                assessmentId,
                userId,
                gymId);

        if (assessment is null)
        {
            return NotFound(new
            {
                message = "Avaliação física não encontrada."
            });
        }

        return Ok(assessment);
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(gymIdClaim, out gymId);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            userIdClaim,
            out userId);
    }
}