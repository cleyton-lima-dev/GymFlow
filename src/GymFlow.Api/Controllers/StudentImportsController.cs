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
public sealed class StudentImportsController(
    StudentImportService imports,
    ILogger<StudentImportsController> logger) : ControllerBase
{
    private static readonly EventId DatabaseFailureEvent = new(13001, "StudentImportDatabaseFailure");

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
        catch (StudentImportStateConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (Exception exception) when (TryClassifyDatabaseFailure(exception, out _))
        {
            return MapDatabaseFailure(exception);
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
        catch (StudentImportStateConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "A importação foi alterada por outra operação. Gere uma nova prévia." });
        }
        catch (Exception exception) when (TryClassifyDatabaseFailure(exception, out _))
        {
            return MapDatabaseFailure(exception);
        }
    }

    private IActionResult MapDatabaseFailure(Exception exception)
    {
        if (!TryClassifyDatabaseFailure(exception, out var failure))
            throw new InvalidOperationException("Database failure classification changed between filter and handler.");

        var exceptionChain = string.Join(" -> ", EnumerateExceptionChain(exception)
            .Take(8)
            .Select(item => item.GetType().Name));
        logger.LogWarning(DatabaseFailureEvent,
            "Student import database failure classified. Category={Category}; SqlState={SqlState}; ExceptionChain={ExceptionChain}",
            failure.Category, failure.SqlState ?? "none", exceptionChain);

        return StatusCode(failure.StatusCode, new { message = failure.SafeMessage });
    }

    private static bool TryClassifyDatabaseFailure(
        Exception exception,
        out DatabaseFailureClassification classification)
    {
        var chain = EnumerateExceptionChain(exception).Take(16).ToArray();
        var postgres = chain.OfType<PostgresException>().FirstOrDefault();
        if (postgres is not null)
        {
            if (postgres.SqlState == "40001")
            {
                classification = new("postgres_serialization_40001", StatusCodes.Status409Conflict,
                    "A importação concorreu com outra operação. Gere uma nova prévia.", postgres.SqlState);
                return true;
            }

            if (postgres.SqlState == "40P01")
            {
                classification = new("postgres_deadlock_40P01", StatusCodes.Status503ServiceUnavailable,
                    "O banco está processando outra operação. Tente novamente em instantes.", postgres.SqlState);
                return true;
            }

            if (postgres.SqlState.StartsWith("08", StringComparison.Ordinal))
            {
                classification = new("postgres_connection_failure", StatusCodes.Status503ServiceUnavailable,
                    "O serviço de dados está temporariamente indisponível. Tente novamente.", postgres.SqlState);
                return true;
            }

            if (postgres.IsTransient)
            {
                classification = new("postgres_transient_failure", StatusCodes.Status503ServiceUnavailable,
                    "O banco está temporariamente indisponível. Tente novamente.", postgres.SqlState);
                return true;
            }

            classification = default!;
            return false;
        }

        var transientProviderException = chain.OfType<NpgsqlException>()
            .FirstOrDefault(item => item.IsTransient);
        if (transientProviderException is not null)
        {
            classification = new("postgres_connection_failure", StatusCodes.Status503ServiceUnavailable,
                "O serviço de dados está temporariamente indisponível. Tente novamente.", null);
            return true;
        }

        classification = default!;
        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            yield return current;
    }

    private sealed record DatabaseFailureClassification(
        string Category,
        int StatusCode,
        string SafeMessage,
        string? SqlState);

    private bool TryGetGymId(out Guid gymId) =>
        Guid.TryParse(User.FindFirst("gym_id")?.Value, out gymId);
}
