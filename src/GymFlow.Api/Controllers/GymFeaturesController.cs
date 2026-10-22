using GymFlow.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/gym-features")]
[Authorize(Roles = "Admin")]
public sealed class GymFeaturesController(IGymErpPolicy erpPolicy) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        if (!Guid.TryParse(User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty)
            return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        return Ok(new { erpEnabled = erpPolicy.IsEnabled(gymId) });
    }
}
