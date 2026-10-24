using System.Security.Claims;
using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agent/physical-access")]
[Authorize(Policy = "AccessAgentOnly")]
public class AccessAgentPhysicalAccessController : ControllerBase
{
    private readonly PhysicalAccessService _physicalAccessService;

    public AccessAgentPhysicalAccessController(PhysicalAccessService physicalAccessService)
    {
        _physicalAccessService = physicalAccessService;
    }

    [HttpPost("decisions")]
    public async Task<IActionResult> Decide(PhysicalAccessDecisionRequest request)
    {
        if (!TryGetIdentity(out var gymId, out var agentId))
            return Unauthorized();

        try
        {
            var response = await _physicalAccessService.DecideAsync(gymId, agentId, request);
            return Ok(response);
        }
        catch (PhysicalAccessOriginConflictException)
        {
            return Conflict(new { message = "Agente ou dispositivo não corresponde à origem do evento." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Decisão de PIN indisponível." });
        }
    }

    [HttpPost("offline-events")]
    public async Task<IActionResult> SyncOfflineEvent(
        [FromBody] SyncOfflinePhysicalAccessEventRequest request)
    {
        if (!TryGetIdentity(out var gymId, out var agentId))
            return Unauthorized();

        try
        {
            var response = await _physicalAccessService.SyncOfflineEventAsync(
                gymId, agentId, request);
            return Ok(response);
        }
        catch (PhysicalAccessOriginConflictException)
        {
            return Conflict(new { message = "Agente ou dispositivo não corresponde à origem do evento." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetIdentity(out Guid gymId, out Guid agentId)
    {
        gymId = Guid.Empty;
        agentId = Guid.Empty;
        return Guid.TryParse(User.FindFirst("gym_id")?.Value, out gymId) &&
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out agentId) &&
            User.FindFirst("identity_type")?.Value == "access_agent";
    }
}
