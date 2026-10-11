using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agent/physical-access")]
[Authorize(Policy = "AccessAgentOnly")]
public class AccessAgentPhysicalAccessController : ControllerBase
{
    private readonly PhysicalAccessService
        _physicalAccessService;

    public AccessAgentPhysicalAccessController(
        PhysicalAccessService physicalAccessService)
    {
        _physicalAccessService = physicalAccessService;
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
                await _physicalAccessService
                    .DecideAsync(
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
    }

    [HttpPost("offline-events")]
    public async Task<IActionResult> SyncOfflineEvent(
    [FromBody] SyncOfflinePhysicalAccessEventRequest request)
    {
        var gymIdClaim =
            User.FindFirst("gym_id")?.Value;

        if (!Guid.TryParse(
                gymIdClaim,
                out var gymId))
        {
            return Unauthorized();
        }

        try
        {
            var response =
                await _physicalAccessService
                    .SyncOfflineEventAsync(
                        gymId,
                        request);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    message = ex.Message
                });
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