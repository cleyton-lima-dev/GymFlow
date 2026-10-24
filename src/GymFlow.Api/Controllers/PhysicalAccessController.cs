using GymFlow.Domain.Enums;
using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/physical-access")]
[Authorize(Roles = "Admin")]
public class PhysicalAccessController : ControllerBase
{
    private readonly PhysicalAccessService
        _physicalAccessService;

    public PhysicalAccessController(
        PhysicalAccessService physicalAccessService)
    {
        _physicalAccessService = physicalAccessService;
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] Guid? studentId,
        [FromQuery] PhysicalAccessDecision? decision,
        [FromQuery] PhysicalAccessDecisionSource? source,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _physicalAccessService
                .GetEventHistoryAsync(
                    gymId,
                    studentId,
                    decision,
                    source,
                    fromDate,
                    toDate,
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
    [HttpPost("decisions")]
    public async Task<IActionResult> Decide(
        PhysicalAccessDecisionRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var response =
                await _physicalAccessService.DecideAsync(
                    gymId,
                    request);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Decisão de PIN indisponível." });
        }
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim =
            User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(
            gymIdClaim,
            out gymId);
    }
}
