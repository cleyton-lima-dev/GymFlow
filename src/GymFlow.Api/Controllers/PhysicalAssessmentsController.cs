using GymFlow.Application.DTOs.PhysicalAssessments;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/students/{studentId:guid}/physical-assessments")]
[Authorize(Roles = "Admin,Professor")]
public class PhysicalAssessmentsController : ControllerBase
{
    private readonly PhysicalAssessmentService _physicalAssessmentService;

    public PhysicalAssessmentsController(
        PhysicalAssessmentService physicalAssessmentService)
    {
        _physicalAssessmentService = physicalAssessmentService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid studentId,
        CreatePhysicalAssessmentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _physicalAssessmentService.CreateAsync(
                studentId,
                gymId,
                request);

            return result switch
            {
                CreatePhysicalAssessmentResult.Success =>
                    StatusCode(201, new
                    {
                        message = "Avaliação física criada com sucesso."
                    }),

                CreatePhysicalAssessmentResult.StudentNotFound =>
                    NotFound(new
                    {
                        message = "Aluno não encontrado."
                    }),

                CreatePhysicalAssessmentResult.AssessmentAlreadyExistsForDate =>
                    Conflict(new
                    {
                        message =
                            "Já existe uma avaliação física para este aluno nesta data."
                    }),

                _ => StatusCode(500)
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(Guid studentId)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var assessment = await _physicalAssessmentService
            .GetLatestAsync(studentId, gymId);

        if (assessment is null)
        {
            return NotFound(new
            {
                message = "Nenhuma avaliação física encontrada."
            });
        }

        return Ok(assessment);
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        Guid studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var history = await _physicalAssessmentService
                .GetHistoryAsync(
                    studentId,
                    gymId,
                    page,
                    pageSize);

            return Ok(history);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{assessmentId:guid}")]
    public async Task<IActionResult> GetById(
        Guid studentId,
        Guid assessmentId)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var assessment = await _physicalAssessmentService
            .GetByIdAsync(
                assessmentId,
                studentId,
                gymId);

        if (assessment is null)
        {
            return NotFound(new
            {
                message = "Avaliação física não encontrada."
            });
        }

        return Ok(assessment);
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(gymIdClaim, out gymId);
    }
}