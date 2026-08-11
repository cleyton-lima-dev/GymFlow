using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = "Admin")]
public class StudentsController : ControllerBase
{
    private readonly StudentService _studentService;

    public StudentsController(StudentService studentService)
    {
        _studentService = studentService;
    }

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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var students = await _studentService.GetAllAsync(gymId);

        return Ok(students);
    }

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

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(gymIdClaim, out gymId);
    }
}