using System.Security.Claims;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agents")]
[Authorize(Roles = "Admin")]
public class AccessAgentManagementController :
    ControllerBase
{
    private readonly AccessAgentControlService
        _controlService;

    public AccessAgentManagementController(
        AccessAgentControlService controlService)
    {
        _controlService = controlService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var result =
            await _controlService
                .ListAsync(gymId);

        return Ok(result);
    }

    [HttpPatch("{agentId:guid}/release")]
    public async Task<IActionResult> SetRelease(
        Guid agentId,
        UpdateAccessAgentReleaseRequest request)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _controlService
                    .SetReleaseEnabledAsync(
                        gymId,
                        userId,
                        agentId,
                        request.Enabled);

            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
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

    private bool TryGetUserId(
        out Guid userId)
    {
        var value =
            User.FindFirst(
                ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            value,
            out userId);
    }
}
