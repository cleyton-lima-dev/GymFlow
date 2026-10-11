using System.Security.Claims;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/access-agents")]
public class AccessAgentsController :
    ControllerBase
{
    private readonly AccessAgentService
        _accessAgentService;

    private readonly AccessAgentPairingService
        _pairingService;

    public AccessAgentsController(
        AccessAgentService accessAgentService,
        AccessAgentPairingService pairingService)
    {
        _accessAgentService =
            accessAgentService;

        _pairingService =
            pairingService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("pairing-code")]
    public async Task<IActionResult>
        CreatePairingCode()
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _pairingService
                .CreateCodeAsync(
                    gymId,
                    userId);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Não foi possível gerar o código de pareamento."
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("pair")]
    public async Task<IActionResult> Pair(
        PairAccessAgentRequest request)
    {
        PairAccessAgentResponse? result;

        try
        {
            result =
                await _pairingService
                    .PairAsync(request);
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
            return Unauthorized(new
            {
                message =
                    "Código de pareamento inválido ou expirado."
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        AccessAgentLoginRequest request)
    {
        var result =
            await _accessAgentService
                .LoginAsync(request);

        if (result is null)
        {
            return Unauthorized(new
            {
                message =
                    "Credenciais do agente inválidas."
            });
        }

        return Ok(result);
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
