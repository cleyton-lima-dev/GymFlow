using System.Security.Claims;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GymFlow.Api.Maintenance;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
    private readonly GymMaintenanceService _maintenance;
    private readonly AppDbContext _db;
    private readonly IAccessAgentPairingCodeService _codes;

    public AccessAgentsController(
        AccessAgentService accessAgentService,
        AccessAgentPairingService pairingService,
        GymMaintenanceService maintenance,
        AppDbContext db,
        IAccessAgentPairingCodeService codes)
    {
        _accessAgentService =
            accessAgentService;

        _pairingService =
            pairingService;
        _maintenance = maintenance;
        _db = db;
        _codes = codes;
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
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var hash = _codes.Hash(request.Code);
            var gym = await _db.AccessAgentPairingCodes.AsNoTracking().Where(x => x.CodeHash == hash)
                .Select(x => (Guid?)x.GymId).SingleOrDefaultAsync();
            if (gym is { } id && (await _maintenance.GetAsync(id))?.IsActive == true)
                return StatusCode(503, new { code = "GYM_MAINTENANCE", message = "Academia temporariamente em manutenção." });
        }
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

        if (result is not null && (await _maintenance.GetAsync(result.GymId))?.IsActive == true)
            return StatusCode(503, new { code = "GYM_MAINTENANCE", message = "Academia temporariamente em manutenção." });

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
