using System.Security.Claims;
using GymFlow.Application.DTOs.CheckIns;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/check-ins")]
[Authorize(Roles = "Admin")]
public class CheckInsController :
    ControllerBase
{
    private readonly CheckInService
        _checkInService;

    public CheckInsController(
        CheckInService checkInService)
    {
        _checkInService =
            checkInService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid? studentId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result =
                await _checkInService
                    .GetHistoryAsync(
                        gymId,
                        studentId,
                        fromDate,
                        toDate,
                        page,
                        pageSize);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("manual")]
    public async Task<IActionResult> CreateManual(
        [FromBody]
        CreateManualCheckInRequest request)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _checkInService
                    .CreateManualAsync(
                        gymId,
                        userId,
                        request);

            if (result is null)
            {
                return NotFound(new
                {
                    message =
                        "Aluno não encontrado."
                });
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
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