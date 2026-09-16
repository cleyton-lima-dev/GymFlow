using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Application.Services;
using GymFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = "Admin")]
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
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var enrollment =
            await _enrollmentService.CreateAsync(
                gymId,
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
        [FromQuery] EnrollmentStatus? status)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var enrollments =
            await _enrollmentService.ListAsync(
                gymId,
                status);

        return Ok(enrollments);
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var cancelled =
            await _enrollmentService.CancelAsync(
                gymId,
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
}