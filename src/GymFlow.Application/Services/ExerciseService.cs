using GymFlow.Application.DTOs.Exercises;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class ExerciseService
{
    private readonly IExerciseRepository _exerciseRepository;

    public ExerciseService(IExerciseRepository exerciseRepository)
    {
        _exerciseRepository = exerciseRepository;
    }

    public async Task<bool> CreateAsync(
        Guid gymId,
        CreateExerciseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return false;

        if (string.IsNullOrWhiteSpace(request.MuscleGroup))
            return false;

        var normalizedName = request.Name.Trim();

        var existingExercise = await _exerciseRepository
            .GetByNameAsync(normalizedName, gymId);

        if (existingExercise is not null)
            return false;

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = normalizedName,
            MuscleGroup = request.MuscleGroup.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _exerciseRepository.AddAsync(exercise);

        return true;
    }

    public async Task<List<ExerciseResponse>> ListAsync(
    Guid gymId,
    string? search = null,
    string? muscleGroup = null,
    bool? isActive = null)
    {
        var exercises = await _exerciseRepository.GetAllByGymAsync(
            gymId,
            search,
            muscleGroup,
            isActive);

        return exercises
            .Select(exercise => new ExerciseResponse
            {
                Id = exercise.Id,
                Name = exercise.Name,
                MuscleGroup = exercise.MuscleGroup,
                Description = exercise.Description,
                IsActive = exercise.IsActive,
                CreatedAt = exercise.CreatedAt,
                UpdatedAt = exercise.UpdatedAt
            })
            .ToList();
    }

    public async Task<bool> UpdateAsync(
    Guid gymId,
    Guid exerciseId,
    UpdateExerciseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return false;

        if (string.IsNullOrWhiteSpace(request.MuscleGroup))
            return false;

        var exercise = await _exerciseRepository
            .GetByIdAsync(exerciseId, gymId);

        if (exercise is null)
            return false;

        var normalizedName = request.Name.Trim();

        var exerciseWithSameName = await _exerciseRepository
            .GetByNameAsync(normalizedName, gymId);

        if (exerciseWithSameName is not null &&
            exerciseWithSameName.Id != exerciseId)
        {
            return false;
        }

        exercise.Name = normalizedName;
        exercise.MuscleGroup = request.MuscleGroup.Trim();
        exercise.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        exercise.UpdatedAt = DateTime.UtcNow;

        await _exerciseRepository.UpdateAsync(exercise);

        return true;
    }

    public async Task<bool> UpdateStatusAsync(
    Guid gymId,
    Guid exerciseId,
    bool isActive)
    {
        var exercise = await _exerciseRepository
            .GetByIdAsync(exerciseId, gymId);

        if (exercise is null)
            return false;

        exercise.IsActive = isActive;
        exercise.UpdatedAt = DateTime.UtcNow;

        await _exerciseRepository.UpdateAsync(exercise);

        return true;
    }
}