using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Application.Services;
using GymFlow.Api.Authorization;
using GymFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = "Admin")]
[ServiceFilter(typeof(ErpEnabledFilter))]
public class EnrollmentsController : ControllerBase
{
    private readonly EnrollmentService _enrollmentService;

    public EnrollmentsController(
        EnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateEnrollmentRequest request)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var enrollment =
            await _enrollmentService.CreateAsync(
                gymId,
                userId,
                request);

        if (enrollment is null)
        {
            return Conflict(new
            {
                message =
                    "Não foi possível criar a matrícula."
            });
        }

        return StatusCode(201, enrollment);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] EnrollmentStatus? status,
        [FromQuery] int? expiringWithinDays)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        if (expiringWithinDays is < 1 or > 365)
            return BadRequest(new { message = "O prazo deve estar entre 1 e 365 dias." });

        var enrollments =
            await _enrollmentService.ListAsync(
                gymId,
                status,
                expiringWithinDays);

        return Ok(enrollments);
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var cancelled =
            await _enrollmentService.CancelAsync(
                gymId,
                userId,
                id);

        if (!cancelled)
        {
            return Conflict(new
            {
                message =
                    "Não foi possível cancelar a matrícula."
            });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/renew")]
    public async Task<IActionResult> Renew(
    Guid id,
    RenewEnrollmentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var enrollment =
            await _enrollmentService.RenewAsync(
                gymId,
                id,
                request);

        if (enrollment is null)
        {
            return Conflict(new
            {
                message =
                    "Não foi possível renovar a matrícula."
            });
        }

        return StatusCode(201, enrollment);
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
        var userIdClaim = User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            userIdClaim,
            out userId);
    }
}
