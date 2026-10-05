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