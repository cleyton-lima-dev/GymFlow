using GymFlow.Application.DTOs.CheckIns;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agent/check-ins")]
[Authorize(Policy = "AccessAgentOnly")]
public class AccessAgentCheckInsController :
    ControllerBase
{
    private readonly CheckInService
        _checkInService;

    public AccessAgentCheckInsController(
        CheckInService checkInService)
    {
        _checkInService =
            checkInService;
    }

    [HttpPost("physical-passage")]
    public async Task<IActionResult>
        ConfirmPhysicalPassage(
            [FromBody]
            ConfirmPhysicalPassageRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result =
                await _checkInService
                    .CreateFromPhysicalPassageAsync(
                        gymId,
                        request);

            if (result is null)
            {
                return NotFound(new
                {
                    message =
                        "Evento de acesso não encontrado."
                });
            }

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

    private bool TryGetGymId(
        out Guid gymId)
    {
        var value =
            User.FindFirst(
                "gym_id")?.Value;

        return Guid.TryParse(
            value,
            out gymId);
    }
}