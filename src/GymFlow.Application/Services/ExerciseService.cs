using GymFlow.Application.DTOs.Exercises;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Application.DTOs.Common;

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

    public async Task<PagedResponse<ExerciseResponse>> ListAsync(
    Guid gymId,
    string? search = null,
    string? muscleGroup = null,
    bool? isActive = null,
    int page = 1,
    int pageSize = 20)
    {
        if (page < 1)
        {
            throw new ArgumentException(
                "A página deve ser maior ou igual a 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentException(
                "O tamanho da página deve estar entre 1 e 100.");
        }

        var skip = (page - 1) * pageSize;

        var (exercises, totalCount) =
            await _exerciseRepository.GetPagedByGymAsync(
                gymId,
                search,
                muscleGroup,
                isActive,
                skip,
                pageSize);

        var items = exercises
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

        return new PagedResponse<ExerciseResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
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