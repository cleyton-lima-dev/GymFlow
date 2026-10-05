using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/physical-access/overrides")]
[Authorize(Roles = "Admin")]
public class PhysicalAccessOverridesController : ControllerBase
{
    private readonly PhysicalAccessOverrideService
        _overrideService;

    public PhysicalAccessOverridesController(
        PhysicalAccessOverrideService overrideService)
    {
        _overrideService = overrideService;
    }

    [HttpPut("student/{studentId:guid}")]
    public async Task<IActionResult> Set(
        Guid studentId,
        SetPhysicalAccessOverrideRequest request)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var accessOverride =
                await _overrideService.SetAsync(
                    gymId,
                    userId,
                    studentId,
                    request);

            if (accessOverride is null)
            {
                return Conflict(new
                {
                    message =
                        "Não foi possível definir a exceção de acesso."
                });
            }

            return Ok(accessOverride);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("student/{studentId:guid}")]
    public async Task<IActionResult> Get(
        Guid studentId)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var accessOverride =
            await _overrideService.GetAsync(
                gymId,
                studentId);

        if (accessOverride is null)
            return NotFound();

        return Ok(accessOverride);
    }

    [HttpDelete("student/{studentId:guid}")]
    public async Task<IActionResult> Remove(
        Guid studentId)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var removed =
            await _overrideService.RemoveAsync(
                gymId,
                studentId);

        if (!removed)
        {
            return NotFound(new
            {
                message =
                    "Nenhuma exceção manual de acesso foi encontrada."
            });
        }

        return NoContent();
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim =
            User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(
            gymIdClaim,
            out gymId);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            userIdClaim,
            out userId);
    }
}