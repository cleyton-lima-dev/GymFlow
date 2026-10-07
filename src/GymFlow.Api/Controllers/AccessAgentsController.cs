using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agents")]
public class AccessAgentsController : ControllerBase
{
    private readonly AccessAgentService _accessAgentService;

    public AccessAgentsController(
        AccessAgentService accessAgentService)
    {
        _accessAgentService = accessAgentService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateAccessAgentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        CreateAccessAgentResponse? result;

        try
        {
            result = await _accessAgentService
                .CreateAsync(
                    gymId,
                    request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }

        if (result is null)
        {
            return Conflict(new
            {
                message =
                    "Não foi possível criar o agente. Verifique os dados ou se já existe um agente para esta máquina."
            });
        }

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
    AccessAgentLoginRequest request)
    {
        var result =
            await _accessAgentService.LoginAsync(request);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Credenciais do agente inválidas."
            });
        }

        return Ok(result);
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