using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/physical-access/credentials")]
[Authorize(Roles = "Admin")]
public class PhysicalAccessCredentialsController : ControllerBase
{
    private readonly PhysicalAccessCredentialService
        _credentialService;

    public PhysicalAccessCredentialsController(
        PhysicalAccessCredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreatePhysicalAccessCredentialRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var credential =
                await _credentialService.CreateAsync(
                    gymId,
                    request);

            if (credential is null)
            {
                return Conflict(new
                {
                    message =
                        "Não foi possível cadastrar a credencial de acesso."
                });
            }

            return StatusCode(
                StatusCodes.Status201Created,
                credential);
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
    public async Task<IActionResult> GetByStudent(
        Guid studentId)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var credentials =
            await _credentialService
                .ListByStudentAsync(
                    gymId,
                    studentId);

        return Ok(credentials);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdatePhysicalAccessCredentialStatusRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var updated =
            await _credentialService
                .UpdateStatusAsync(
                    gymId,
                    id,
                    request.IsActive);

        if (!updated)
        {
            return NotFound(new
            {
                message =
                    "Credencial de acesso não encontrada."
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
}