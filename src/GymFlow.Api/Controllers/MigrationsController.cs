using System.Security.Claims;
using GymFlow.Api.Services;
using GymFlow.Api.Authorization;
using GymFlow.Application.DTOs.Migrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/migrations")]
[Authorize(Roles = "Admin")]
[ServiceFilter(typeof(ErpEnabledFilter))]
public sealed class MigrationsController(MigrationService migrations) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out _)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await migrations.ListSessionsAsync(gymId, token));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMigrationSessionRequest request, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out var actorId)) return Unauthorized();
        try
        {
            var session = await migrations.CreateSessionAsync(gymId, actorId, request, token);
            return CreatedAtAction(nameof(Get), new { sessionId = session.Id }, new
            {
                session.Id, session.Name, session.SourceNamespace, session.Status, session.ExpiresAt
            });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "A sessão foi alterada por outra operação. Tente novamente." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "A sessão foi alterada por outra operação. Tente novamente." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "A sessão foi alterada por outra operação. Tente novamente." }); }
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out _)) return Unauthorized();
        try { Response.Headers.CacheControl = "no-store"; return Ok(await migrations.GetSessionAsync(gymId, sessionId, token)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("{sessionId:guid}/preview")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Preview(Guid sessionId, PreviewMigrationBatchRequest request, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out _)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await migrations.PreviewAsync(gymId, sessionId, request, token)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "A sessão mudou durante a prévia. Atualize a sessão e tente novamente." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "A sessão mudou durante a prévia. Atualize a sessão e tente novamente." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "A sessão mudou durante a prévia. Atualize a sessão e tente novamente." }); }
    }

    [HttpGet("{sessionId:guid}/batches/{batchId:guid}/review")]
    public async Task<IActionResult> GetBatchReview(Guid sessionId, Guid batchId, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out _)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await migrations.GetBatchReviewAsync(gymId, sessionId, batchId, token)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "A sessão ou o lote mudou durante a revisão." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "A sessão ou o lote mudou durante a revisão." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "A sessão ou o lote mudou durante a revisão." }); }
    }

    [HttpPost("{sessionId:guid}/batches/{batchId:guid}/review")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> SaveBatchReview(Guid sessionId, Guid batchId, ReviewMigrationBatchRequest request, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out var actorId)) return Unauthorized();
        try { await migrations.SaveBatchReviewAsync(gymId, sessionId, batchId, request, token, actorId); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
    }

    [HttpPost("{sessionId:guid}/batches/{batchId:guid}/confirm")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> ConfirmBatch(Guid sessionId, Guid batchId, ReviewMigrationBatchRequest request, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out var actorId)) return Unauthorized();
        try { await migrations.ConfirmBatchAsync(gymId, sessionId, batchId, request, token, actorId); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
    }

    [HttpPost("{sessionId:guid}/batches/{batchId:guid}/discard")]
    public async Task<IActionResult> DiscardBatch(Guid sessionId, Guid batchId, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out _)) return Unauthorized();
        try { await migrations.DiscardBatchAsync(gymId, sessionId, batchId, token); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "O lote foi alterado por outra operação." }); }
    }

    [HttpPost("{sessionId:guid}/release")]
    [RequestSizeLimit(100_000)]
    public async Task<IActionResult> Release(Guid sessionId, CancellationToken token)
    {
        if (!TryGetIdentity(out var gymId, out var actorId)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await migrations.ReleaseAsync(gymId, sessionId, actorId, token)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "A sessão foi alterada por outra operação." }); }
        catch (DbUpdateException exception) when (IsConcurrentPostgresUpdate(exception))
        { return Conflict(new { message = "Outra liberação concorrente usou este identificador externo." }); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01" or "23505")
        { return Conflict(new { message = "Outra liberação concorrente usou este identificador externo." }); }
    }

    private static bool IsConcurrentPostgresUpdate(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: "23505" or "40001" or "40P01" };

    private bool TryGetIdentity(out Guid gymId, out Guid actorId)
    {
        var hasGym = Guid.TryParse(User.FindFirst("gym_id")?.Value, out gymId);
        var hasActor = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId);
        return hasGym && hasActor;
    }

}
