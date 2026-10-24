using System.Security.Claims;
using GymFlow.Application.DTOs.CheckIns;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agent/check-ins")]
[Authorize(Policy = "AccessAgentOnly")]
public class AccessAgentCheckInsController : ControllerBase
{
    private readonly CheckInService _checkInService;

    public AccessAgentCheckInsController(CheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    [HttpPost("physical-passage")]
    public async Task<IActionResult> ConfirmPhysicalPassage(
        [FromBody] ConfirmPhysicalPassageRequest request)
    {
        if (!TryGetIdentity(out var gymId, out var agentId))
            return Unauthorized();

        try
        {
            var result = await _checkInService.CreateFromPhysicalPassageAsync(
                gymId, agentId, request);

            if (result is null)
                return NotFound(new { message = "Evento de acesso não encontrado." });

            return Ok(result);
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

    private bool TryGetIdentity(out Guid gymId, out Guid agentId)
    {
        gymId = Guid.Empty;
        agentId = Guid.Empty;
        return Guid.TryParse(User.FindFirst("gym_id")?.Value, out gymId) &&
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out agentId) &&
            User.FindFirst("identity_type")?.Value == "access_agent";
    }
}
