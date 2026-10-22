using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using GymFlow.Api.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin")]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet]
    [ServiceFilter(typeof(ErpEnabledFilter))]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty)
            return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.GetAsync(gymId, cancellationToken));
    }

    [HttpGet("operational")]
    public async Task<IActionResult> GetOperational(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty)
            return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.GetOperationalAsync(gymId, cancellationToken));
    }
}
