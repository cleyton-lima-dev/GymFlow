using GymFlow.Application.Services;
using GymFlow.Application.DTOs.GymSettings;
using GymFlow.Application.Exceptions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/gym-settings")]
[Authorize(Roles = "Admin")]
public sealed class GymSettingsController(GymSettingsService service) : ControllerBase
{
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Get()
    {
        if (!Guid.TryParse(User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty)
            return Unauthorized();
        return Ok(await service.GetAsync(gymId));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateGymSettingsRequest request)
    {
        if (!Guid.TryParse(User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty ||
            !Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId) || actorId == Guid.Empty)
            return Unauthorized();
        try { return Ok(await service.UpdateAsync(gymId, actorId, request)); }
        catch (GymSettingsConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
