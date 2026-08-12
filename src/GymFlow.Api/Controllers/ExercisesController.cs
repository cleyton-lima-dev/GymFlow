using GymFlow.Application.DTOs.Exercises;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/exercises")]
[Authorize(Roles = "Admin")]
public class ExercisesController : ControllerBase
{
    private readonly ExerciseService _exerciseService;

    public ExercisesController(ExerciseService exerciseService)
    {
        _exerciseService = exerciseService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateExerciseRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var created = await _exerciseService.CreateAsync(
            gymId,
            request);

        if (!created)
        {
            return Conflict(new
            {
                message = "Não foi possível cadastrar o exercício."
            });
        }

        return StatusCode(201, new
        {
            message = "Exercício criado com sucesso."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? muscleGroup,
        [FromQuery] bool? isActive)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var exercises = await _exerciseService.ListAsync(
            gymId,
            search,
            muscleGroup,
            isActive);

        return Ok(exercises);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateExerciseRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var updated = await _exerciseService.UpdateAsync(
            gymId,
            id,
            request);

        if (!updated)
        {
            return Conflict(new
            {
                message = "Não foi possível atualizar o exercício."
            });
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateExerciseStatusRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var updated = await _exerciseService.UpdateStatusAsync(
            gymId,
            id,
            request.IsActive);

        if (!updated)
        {
            return NotFound(new
            {
                message = "Exercício não encontrado."
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