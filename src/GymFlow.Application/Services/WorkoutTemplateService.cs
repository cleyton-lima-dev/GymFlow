using GymFlow.Application.DTOs.WorkoutTemplates;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class WorkoutTemplateService
{
    private readonly IWorkoutTemplateRepository _workoutTemplateRepository;
    private readonly IExerciseRepository _exerciseRepository;

    public WorkoutTemplateService(
        IWorkoutTemplateRepository workoutTemplateRepository,
        IExerciseRepository exerciseRepository)
    {
        _workoutTemplateRepository = workoutTemplateRepository;
        _exerciseRepository = exerciseRepository;
    }

    public async Task<WorkoutTemplateResponse> CreateAsync(
        Guid gymId,
        CreateWorkoutTemplateRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do modelo é obrigatório.");

        if (request.Days.Count == 0)
            throw new ArgumentException("O modelo deve possuir pelo menos um dia.");

        var exists = await _workoutTemplateRepository
            .ExistsByNameAsync(gymId, name);

        if (exists)
            throw new InvalidOperationException(
                "Já existe um modelo de treino com esse nome.");

        ValidateDays(request.Days);

        var exerciseIds = request.Days
    .SelectMany(x => x.Exercises)
    .Select(x => x.ExerciseId)
    .Distinct()
    .ToList();

        var exercises = await _exerciseRepository
            .GetByIdsAsync(exerciseIds, gymId);

        var foundExerciseIds = exercises
            .Select(x => x.Id)
            .ToHashSet();

        var missingExerciseId = exerciseIds
            .FirstOrDefault(x => !foundExerciseIds.Contains(x));

        if (missingExerciseId != Guid.Empty)
        {
            throw new ArgumentException(
                $"Exercício '{missingExerciseId}' não encontrado.");
        }

        var inactiveExercise = exercises
            .FirstOrDefault(x => !x.IsActive);

        if (inactiveExercise is not null)
        {
            throw new InvalidOperationException(
                $"O exercício '{inactiveExercise.Name}' está inativo.");
        }

        var exercisesById = exercises.ToDictionary(x => x.Id);

        var workoutTemplate = new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var dayRequest in request.Days.OrderBy(x => x.Order))
        {
            var day = new WorkoutTemplateDay
            {
                Id = Guid.NewGuid(),
                WorkoutTemplateId = workoutTemplate.Id,
                Name = dayRequest.Name.Trim(),
                Order = dayRequest.Order
            };

            foreach (var exerciseRequest in
                     dayRequest.Exercises.OrderBy(x => x.Order))
            {
                var exercise = exercisesById[exerciseRequest.ExerciseId];

                day.Exercises.Add(new WorkoutTemplateExercise
                {
                    Id = Guid.NewGuid(),
                    WorkoutTemplateDayId = day.Id,
                    ExerciseId = exercise.Id,
                    Sets = exerciseRequest.Sets,
                    Repetitions = exerciseRequest.Repetitions.Trim(),
                    RestSeconds = exerciseRequest.RestSeconds,
                    Notes = exerciseRequest.Notes?.Trim(),
                    Order = exerciseRequest.Order
                });
            }

            workoutTemplate.Days.Add(day);
        }

        await _workoutTemplateRepository.AddAsync(workoutTemplate);

        return new WorkoutTemplateResponse
        {
            Id = workoutTemplate.Id,
            Name = workoutTemplate.Name,
            Description = workoutTemplate.Description,
            IsActive = workoutTemplate.IsActive,
            CreatedAt = workoutTemplate.CreatedAt
        };
    }

    public async Task<List<WorkoutTemplateListItemResponse>> GetAllAsync(
    Guid gymId)
    {
        var templates = await _workoutTemplateRepository
            .GetAllByGymAsync(gymId);

        return templates
            .Select(x => new WorkoutTemplateListItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }

    public async Task<WorkoutTemplateDetailResponse?> GetByIdAsync(
    Guid id,
    Guid gymId)
    {
        var template = await _workoutTemplateRepository
            .GetByIdAsync(id, gymId);

        if (template is null)
            return null;

        return new WorkoutTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,

            Days = template.Days
                .OrderBy(x => x.Order)
                .Select(day => new WorkoutTemplateDayResponse
                {
                    Id = day.Id,
                    Name = day.Name,
                    Order = day.Order,

                    Exercises = day.Exercises
                        .OrderBy(x => x.Order)
                        .Select(exercise => new WorkoutTemplateExerciseResponse
                        {
                            Id = exercise.Id,
                            ExerciseId = exercise.ExerciseId,
                            ExerciseName = exercise.Exercise.Name,
                            MuscleGroup = exercise.Exercise.MuscleGroup,
                            Sets = exercise.Sets,
                            Repetitions = exercise.Repetitions,
                            RestSeconds = exercise.RestSeconds,
                            Notes = exercise.Notes,
                            Order = exercise.Order
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public async Task<bool> UpdateAsync(
    Guid id,
    Guid gymId,
    UpdateWorkoutTemplateRequest request)
    {
        var template = await _workoutTemplateRepository
            .GetForUpdateAsync(id, gymId);

        if (template is null)
            return false;

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome do modelo é obrigatório.");

        if (request.Days.Count == 0)
            throw new ArgumentException(
                "O modelo deve possuir pelo menos um dia.");

        var exists = await _workoutTemplateRepository
            .ExistsByNameAsync(gymId, name, id);

        if (exists)
            throw new InvalidOperationException(
                "Já existe outro modelo de treino com esse nome.");

        ValidateUpdateDays(request.Days);

        var exerciseIds = request.Days
            .SelectMany(x => x.Exercises)
            .Select(x => x.ExerciseId)
            .Distinct()
            .ToList();

        var exercises = await _exerciseRepository
            .GetByIdsAsync(exerciseIds, gymId);

        var foundExerciseIds = exercises
            .Select(x => x.Id)
            .ToHashSet();

        var missingExerciseId = exerciseIds
            .FirstOrDefault(x => !foundExerciseIds.Contains(x));

        if (missingExerciseId != Guid.Empty)
        {
            throw new ArgumentException(
                $"Exercício '{missingExerciseId}' não encontrado.");
        }

        var inactiveExercise = exercises
            .FirstOrDefault(x => !x.IsActive);

        if (inactiveExercise is not null)
        {
            throw new InvalidOperationException(
                $"O exercício '{inactiveExercise.Name}' está inativo.");
        }

        var exercisesById = exercises.ToDictionary(x => x.Id);

        template.Name = name;
        template.Description = request.Description?.Trim();
        template.UpdatedAt = DateTime.UtcNow;

        var oldDays = template.Days.ToList();

        await _workoutTemplateRepository.RemoveDaysAsync(oldDays);

        template.Days.Clear();

        foreach (var dayRequest in request.Days.OrderBy(x => x.Order))
        {
            var day = new WorkoutTemplateDay
            {
                Id = Guid.NewGuid(),
                WorkoutTemplateId = template.Id,
                Name = dayRequest.Name.Trim(),
                Order = dayRequest.Order
            };

            foreach (var exerciseRequest in
                     dayRequest.Exercises.OrderBy(x => x.Order))
            {
                var exercise =
                    exercisesById[exerciseRequest.ExerciseId];

                day.Exercises.Add(new WorkoutTemplateExercise
                {
                    Id = Guid.NewGuid(),
                    WorkoutTemplateDayId = day.Id,
                    ExerciseId = exercise.Id,
                    Sets = exerciseRequest.Sets,
                    Repetitions = exerciseRequest.Repetitions.Trim(),
                    RestSeconds = exerciseRequest.RestSeconds,
                    Notes = exerciseRequest.Notes?.Trim(),
                    Order = exerciseRequest.Order
                });
            }

            template.Days.Add(day);
        }
        _workoutTemplateRepository.AddDays(template.Days);

        await _workoutTemplateRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SetActiveStatusAsync(
    Guid id,
    Guid gymId,
    bool isActive)
    {
        return await _workoutTemplateRepository
            .SetActiveStatusAsync(id, gymId, isActive);
    }

    private static void ValidateUpdateDays(
    List<UpdateWorkoutTemplateDayRequest> days)
    {
        if (days.Any(x => string.IsNullOrWhiteSpace(x.Name)))
            throw new ArgumentException(
                "Todos os dias devem possuir um nome.");

        if (days.GroupBy(x => x.Order).Any(x => x.Count() > 1))
            throw new ArgumentException(
                "A ordem dos dias não pode se repetir.");

        foreach (var day in days)
        {
            if (day.Exercises.Count == 0)
                throw new ArgumentException(
                    $"O dia '{day.Name}' deve possuir pelo menos um exercício.");

            if (day.Exercises.Any(x => x.Sets <= 0))
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir pelo menos uma série.");

            if (day.Exercises.Any(
                    x => string.IsNullOrWhiteSpace(x.Repetitions)))
            {
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir repetições.");
            }

            if (day.Exercises.Any(
                    x => x.RestSeconds.HasValue &&
                         x.RestSeconds.Value < 0))
            {
                throw new ArgumentException(
                    $"O tempo de descanso no dia '{day.Name}' não pode ser negativo.");
            }

            if (day.Exercises
                .GroupBy(x => x.Order)
                .Any(x => x.Count() > 1))
            {
                throw new ArgumentException(
                    $"A ordem dos exercícios no dia '{day.Name}' não pode se repetir.");
            }
        }
    }

    private static void ValidateDays(
        List<CreateWorkoutTemplateDayRequest> days)
    {
        if (days.Any(x => string.IsNullOrWhiteSpace(x.Name)))
            throw new ArgumentException(
                "Todos os dias devem possuir um nome.");

        if (days.GroupBy(x => x.Order).Any(x => x.Count() > 1))
            throw new ArgumentException(
                "A ordem dos dias não pode se repetir.");

        foreach (var day in days)
        {
            if (day.Exercises.Count == 0)
                throw new ArgumentException(
                    $"O dia '{day.Name}' deve possuir pelo menos um exercício.");

            if (day.Exercises.Any(x => x.Sets <= 0))
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir pelo menos uma série.");

            if (day.Exercises.Any(
                    x => string.IsNullOrWhiteSpace(x.Repetitions)))
            {
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir repetições.");
            }

            if (day.Exercises.Any(
                    x => x.RestSeconds.HasValue && x.RestSeconds.Value < 0))
            {
                throw new ArgumentException(
                    $"O tempo de descanso no dia '{day.Name}' não pode ser negativo.");
            }

            if (day.Exercises
                .GroupBy(x => x.Order)
                .Any(x => x.Count() > 1))
            {
                throw new ArgumentException(
                    $"A ordem dos exercícios no dia '{day.Name}' não pode se repetir.");
            }
        }
    }


}