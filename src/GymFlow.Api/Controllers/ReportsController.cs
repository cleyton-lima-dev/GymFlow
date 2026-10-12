using GymFlow.Application.DTOs.Reports;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public sealed class ReportsController(ReportService service) : ControllerBase
{
    [HttpGet("{kind}")]
    public Task<IActionResult> Get(string kind, [FromQuery] ReportQuery query, CancellationToken token) =>
        Execute(kind, query, false, token);

    [HttpGet("{kind}/export")]
    public Task<IActionResult> Export(string kind, [FromQuery] ReportQuery query, CancellationToken token) =>
        Execute(kind, query, true, token);

    private async Task<IActionResult> Execute(string kind, ReportQuery query, bool export, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty)
            return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try
        {
            if (export)
                return File(await service.ExportAsync(gymId, kind, query, token), "text/csv; charset=utf-8",
                    $"avelri-{kind}.csv");
            return Ok(await service.GetAsync(gymId, kind, query, cancellationToken: token));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
