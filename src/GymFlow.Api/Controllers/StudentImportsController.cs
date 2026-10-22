using System.Security.Claims;
using GymFlow.Api.Services;
using GymFlow.Api.Authorization;
using GymFlow.Application.DTOs.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/students/imports")]
[Authorize(Roles = "Admin")]
[ServiceFilter(typeof(ErpEnabledFilter))]
public sealed class StudentImportsController(StudentImportService imports) : ControllerBase
{
    [HttpPost("preview")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Preview(
        PreviewStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetGymId(out var gymId)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try
        {
            return Ok(await imports.PreviewAsync(gymId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("{batchId:guid}/confirm")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Confirm(
        Guid batchId,
        ConfirmStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetGymId(out var gymId)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try
        {
            return Ok(await imports.ConfirmAsync(gymId, batchId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (StudentImportReviewChangedException exception)
        {
            return Conflict(new { message = exception.Message, rows = exception.Rows });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "A importação foi alterada por outra operação. Gere uma nova prévia." });
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return Conflict(new { message = "A importação concorreu com outra operação. Gere uma nova prévia." });
        }
    }

    private bool TryGetGymId(out Guid gymId) =>
        Guid.TryParse(User.FindFirst("gym_id")?.Value, out gymId);
}
