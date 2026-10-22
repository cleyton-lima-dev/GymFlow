using GymFlow.Application.DTOs.Reports;
using GymFlow.Application.Services;
using GymFlow.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
[ServiceFilter(typeof(ErpReportFilter))]
public sealed class ReportsController(ReportService service, ErpMetadataAccess erpMetadata) : ControllerBase
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
        var includeEnrollmentMetadata = kind != "students" || erpMetadata.IsEnabled(gymId);
        if (!includeEnrollmentMetadata && !string.IsNullOrWhiteSpace(query.EnrollmentStatus))
            return StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            if (export)
                return File(await service.ExportAsync(gymId, kind, query, token, includeEnrollmentMetadata), "text/csv; charset=utf-8",
                    $"avelri-{kind}.csv");
            return Ok(await service.GetAsync(gymId, kind, query, cancellationToken: token,
                includeEnrollmentMetadata: includeEnrollmentMetadata));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
