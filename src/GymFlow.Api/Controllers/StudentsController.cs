using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly StudentService _studentService;

    public StudentsController(StudentService studentService)
    {
        _studentService = studentService;
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateStudentRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var created = await _studentService.CreateAsync(gymId, request);

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
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        try
        {
            var students = await _studentService.GetAllAsync(
                gymId,
                search,
                isActive,
                page,
                pageSize);

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