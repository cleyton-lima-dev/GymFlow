using System.Security.Claims;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agent/control-plane")]
[Authorize(Policy = "AccessAgentOnly")]
public class AccessAgentControlPlaneController :
    ControllerBase
{
    private readonly AccessAgentControlService
        _controlService;

    public AccessAgentControlPlaneController(
        AccessAgentControlService controlService)
    {
        _controlService = controlService;
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(
        AccessAgentHeartbeatRequest request)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetAgentId(out var agentId))
        {
            return Unauthorized();
        }

        try
        {
            var response =
                await _controlService
                    .HeartbeatAsync(
                        gymId,
                        agentId,
                        request.AppliedConfigurationVersion);

            if (response is null)
                return Unauthorized();

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

    private bool TryGetGymId(
        out Guid gymId)
    {
        var value =
            User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(
            value,
            out gymId);
    }

    private bool TryGetAgentId(
        out Guid agentId)
    {
        var value =
            User.FindFirst(
                ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            value,
            out agentId);
    }
}
