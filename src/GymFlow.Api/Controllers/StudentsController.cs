using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Services;
using GymFlow.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;


namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly StudentService _studentService;
    private readonly EnrollmentService _enrollmentService;
    private readonly ErpMetadataAccess _erpMetadata;

    public StudentsController(
    StudentService studentService,
    EnrollmentService enrollmentService,
    ErpMetadataAccess erpMetadata)
    {
        _studentService = studentService;
        _enrollmentService = enrollmentService;
        _erpMetadata = erpMetadata;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/app-access")]
    public async Task<IActionResult> SetAppAccess(Guid id, SetStudentAppAccessRequest request)
    {
        if (!TryGetGymId(out var gymId)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        try
        {
            var result = await _studentService.SetAppAccessAsync(id, gymId, request.Enabled);
            return result is null ? NotFound() : Ok(new
            {
                enabled = result.Value.Enabled,
                token = result.Value.Token,
                expiresAt = result.Value.ExpiresAt
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("app-activate/validate")]
    public async Task<IActionResult> ValidateAppInvite(ValidateStudentAppInviteRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        var message = await _studentService.ValidateAppInviteAsync(request.Token);
        return message is null
            ? Ok(new { valid = true })
            : BadRequest(new { valid = false, message });
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("app-activate")]
    public async Task<IActionResult> ActivateApp(ActivateStudentAppRequest request)
    {
        try
        {
            return await _studentService.ActivateAppAsync(request.Token, request.Password)
                ? NoContent() : BadRequest(new { message = "Convite inválido ou expirado." });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateStudentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        bool created;

        try
        {
            created = await _studentService.CreateAsync(gymId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }

        if (!created)
        {
            return Conflict(new
            {
                message = "Já existe um usuário com este e-mail."
            });
        }

        return StatusCode(201, new
        {
            message = "Aluno criado com sucesso."
        });
    }


    [Authorize(Roles = "Admin,Professor")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
    [FromQuery] string? search,
    [FromQuery] bool? isActive,
    [FromQuery] StudentEnrollmentFilter? enrollmentFilter,
    [FromQuery] StudentArchiveFilter archiveFilter = StudentArchiveFilter.NotArchived,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        var includeEnrollmentMetadata = User.IsInRole("Admin") && _erpMetadata.IsEnabled(gymId);
        // A filter would reveal the hidden contract through counts and membership.
        if (enrollmentFilter.HasValue && !includeEnrollmentMetadata)
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            var students = await _studentService.GetAllAsync(
                gymId,
                search,
                isActive,
                enrollmentFilter,
                archiveFilter,
                page,
                pageSize,
                includeEnrollmentMetadata);

            return Ok(students);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }


    [Authorize(Roles = "Admin,Professor")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var student = await _studentService.GetByIdAsync(id, gymId);

        if (student is null)
        {
            return NotFound(new
            {
                message = "Aluno não encontrado."
            });
        }

        return Ok(student);
    }


    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
    Guid id,
    UpdateStudentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var result = await _studentService.UpdateAsync(
                id,
                gymId,
                request);

            return result switch
            {
                UpdateStudentResult.Success => NoContent(),

                UpdateStudentResult.NotFound => NotFound(new
                {
                    message = "Aluno não encontrado."
                }),

                UpdateStudentResult.EmailAlreadyInUse => Conflict(new
                {
                    message = "Já existe um usuário com este e-mail."
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


    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateStudentStatusRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var updated = await _studentService.UpdateStatusAsync(
            id,
            gymId,
            request.IsActive);

        if (!updated)
        {
            return NotFound(new
            {
                message = "Aluno não encontrado."
            });
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/reactivate")]
    [ServiceFilter(typeof(ErpEnabledFilter))]
    public async Task<IActionResult> ReactivateArchived(
    Guid id,
    ReactivateArchivedStudentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var enrollment =
            await _enrollmentService
                .ReactivateArchivedStudentAsync(
                    gymId,
                    id,
                    request);

        if (enrollment is null)
        {
            return BadRequest(new
            {
                message =
                    "Não foi possível reativar o aluno."
            });
        }

        return Ok(enrollment);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id:guid}/financial-history")]
    [ServiceFilter(typeof(ErpEnabledFilter))]
    public async Task<IActionResult> GetFinancialHistory(Guid id)
    {
        if (!TryGetGymId(out var gymId))
        {
            return Unauthorized();
        }

        var history = await _enrollmentService
            .GetStudentFinancialHistoryAsync(
                gymId,
                id);

        if (history is null)
        {
            return NotFound(new
            {
                message = "Aluno não encontrado."
            });
        }

        return Ok(history);
    }

    [Authorize(Roles = "Student")]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var student = await _studentService
            .GetMeAsync(userId, gymId);

        if (student is null)
        {
            return NotFound(new
            {
                message = "Aluno não encontrado."
            });
        }

        return Ok(student);
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(gymIdClaim, out gymId);
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
