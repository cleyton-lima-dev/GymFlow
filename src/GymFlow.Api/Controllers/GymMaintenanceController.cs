using GymFlow.Api.Maintenance;
using GymFlow.Api.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/platform/gyms/{gymId:guid}/maintenance")]
[Authorize(Policy = PlatformAuthentication.Policy)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class GymMaintenanceController(GymMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid gymId)
    {
        var state = await service.GetAsync(gymId);
        return Ok(new { active = state?.IsActive == true, allowedAdminUserId = state?.IsActive == true
            ? state.AllowedAdminUserId : null, version = state?.Version ?? 0 });
    }

    [HttpPut]
    public Task<IActionResult> Activate(Guid gymId, ActivateGymMaintenanceRequest request) =>
        Change(gymId, request.AllowedAdminUserId, true);

    [HttpDelete]
    public Task<IActionResult> Deactivate(Guid gymId) => Change(gymId, null, false);

    private async Task<IActionResult> Change(Guid gymId, Guid? adminId, bool activate)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        try
        {
            var state = await service.ChangeAsync(gymId, actorId, adminId, activate);
            return Ok(new { active = state.IsActive, version = state.Version });
        }
        catch (ArgumentException) { return BadRequest(new { message = "Identidade inválida para manutenção." }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (GymMaintenanceConflictException) { return Conflict(new { message = "Estado de manutenção alterado. Consulte o estado atual." }); }
    }
}

public sealed record ActivateGymMaintenanceRequest(Guid AllowedAdminUserId);
