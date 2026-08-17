using GymFlow.Application.DTOs.Workouts;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class WorkoutService
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IWorkoutTemplateRepository _workoutTemplateRepository;

    public WorkoutService(
        IWorkoutRepository workoutRepository,
        IStudentRepository studentRepository,
        IExerciseRepository exerciseRepository,
        IWorkoutTemplateRepository workoutTemplateRepository,
        IWorkoutExecutionRepository workoutExecutionRepository)
    {
        _workoutRepository = workoutRepository;
        _studentRepository = studentRepository;
        _exerciseRepository = exerciseRepository;
        _workoutTemplateRepository = workoutTemplateRepository;
        _workoutExecutionRepository = workoutExecutionRepository;
    }

    public async Task<WorkoutResponse> CreateManualAsync(
        Guid gymId,
        CreateWorkoutRequest request)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(request.StudentId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        if (!student.User.IsActive)
            throw new InvalidOperationException(
                "Não é possível atribuir treino a um aluno inativo.");

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome do treino é obrigatório.");

        if (request.Days.Count == 0)
            throw new ArgumentException(
                "O treino deve possuir pelo menos um dia.");



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

        var exercisesById = exercises
            .ToDictionary(x => x.Id);

        var currentWorkout = await _workoutRepository
            .GetActiveForUpdateAsync(request.StudentId, gymId);

        if (currentWorkout is not null)
        {
            currentWorkout.IsActive = false;
            currentWorkout.UpdatedAt = DateTime.UtcNow;
        }

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            GymId = gymId,
            SourceWorkoutTemplateId = null,
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var dayRequest in request.Days.OrderBy(x => x.Order))
        {
            var day = new WorkoutDay
            {
                Id = Guid.NewGuid(),
                WorkoutId = workout.Id,
                Name = dayRequest.Name.Trim(),
                Order = dayRequest.Order
            };

            foreach (var exerciseRequest in
                     dayRequest.Exercises.OrderBy(x => x.Order))
            {
                var exercise =
                    exercisesById[exerciseRequest.ExerciseId];

                day.Exercises.Add(new WorkoutExercise
                {
                    Id = Guid.NewGuid(),
                    WorkoutDayId = day.Id,
                    ExerciseId = exercise.Id,
                    Sets = exerciseRequest.Sets,
                    Repetitions =
                        exerciseRequest.Repetitions.Trim(),
                    RestSeconds =
                        exerciseRequest.RestSeconds,
                    Notes =
                        exerciseRequest.Notes?.Trim(),
                    Order = exerciseRequest.Order
                });
            }

            workout.Days.Add(day);
        }

        await _workoutRepository.AddAsync(workout);

        await _workoutRepository.SaveChangesAsync();

        return new WorkoutResponse
        {
            Id = workout.Id,
            StudentId = workout.StudentId,
            SourceWorkoutTemplateId =
                workout.SourceWorkoutTemplateId,
            Name = workout.Name,
            Description = workout.Description,
            IsActive = workout.IsActive,
            CreatedAt = workout.CreatedAt
        };
    }

    public async Task<WorkoutResponse> CreateFromTemplateAsync(
    Guid gymId,
    CreateWorkoutFromTemplateRequest request)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(request.StudentId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        if (!student.User.IsActive)
        {
            throw new InvalidOperationException(
                "Não é possível atribuir treino a um aluno inativo.");
        }

        var template = await _workoutTemplateRepository
            .GetByIdAsync(request.TemplateId, gymId);

        if (template is null)
            throw new ArgumentException("Modelo de treino não encontrado.");

        if (!template.IsActive)
        {
            throw new InvalidOperationException(
                "Não é possível utilizar um modelo de treino inativo.");
        }

        if (template.Days.Count == 0)
        {
            throw new InvalidOperationException(
                "O modelo de treino não possui dias.");
        }

        var exerciseIds = template.Days
            .SelectMany(x => x.Exercises)
            .Select(x => x.ExerciseId)
            .Distinct()
            .ToList();

        var exercises = await _exerciseRepository
            .GetByIdsAsync(exerciseIds, gymId);

        var exercisesById = exercises
            .ToDictionary(x => x.Id);

        foreach (var exerciseId in exerciseIds)
        {
            if (!exercisesById.TryGetValue(
                    exerciseId,
                    out var exercise))
            {
                throw new InvalidOperationException(
                    $"O exercício '{exerciseId}' do modelo não foi encontrado.");
            }

            if (!exercise.IsActive)
            {
                throw new InvalidOperationException(
                    $"O exercício '{exercise.Name}' do modelo está inativo.");
            }
        }

        var currentWorkout = await _workoutRepository
            .GetActiveForUpdateAsync(request.StudentId, gymId);

        if (currentWorkout is not null)
        {
            currentWorkout.IsActive = false;
            currentWorkout.UpdatedAt = DateTime.UtcNow;
        }

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            GymId = gymId,

            SourceWorkoutTemplateId = template.Id,

            Name = template.Name,
            Description = template.Description,

            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var templateDay in
                 template.Days.OrderBy(x => x.Order))
        {
            var workoutDay = new WorkoutDay
            {
                Id = Guid.NewGuid(),
                WorkoutId = workout.Id,
                Name = templateDay.Name,
                Order = templateDay.Order
            };

            foreach (var templateExercise in
                     templateDay.Exercises.OrderBy(x => x.Order))
            {
                workoutDay.Exercises.Add(
                    new WorkoutExercise
                    {
                        Id = Guid.NewGuid(),

                        WorkoutDayId = workoutDay.Id,

                        ExerciseId =
                            templateExercise.ExerciseId,

                        Sets =
                            templateExercise.Sets,

                        Repetitions =
                            templateExercise.Repetitions,

                        RestSeconds =
                            templateExercise.RestSeconds,

                        Notes =
                            templateExercise.Notes,

                        Order =
                            templateExercise.Order
                    });
            }

            workout.Days.Add(workoutDay);
        }

        await _workoutRepository.AddAsync(workout);

        await _workoutRepository.SaveChangesAsync();

        return new WorkoutResponse
        {
            Id = workout.Id,
            StudentId = workout.StudentId,

            SourceWorkoutTemplateId =
                workout.SourceWorkoutTemplateId,

            Name = workout.Name,
            Description = workout.Description,
            IsActive = workout.IsActive,
            CreatedAt = workout.CreatedAt
        };
    }

    public async Task<WorkoutExecutionResponse> CompleteDayAsync(
    Guid gymId,
    Guid studentId,
    Guid workoutDayId)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            throw new ArgumentException(
                "Aluno não encontrado.");

        if (!student.User.IsActive)
        {
            throw new InvalidOperationException(
                "Aluno inativo não pode registrar execução de treino.");
        }

        var validWorkoutDay =
            await _workoutExecutionRepository
                .IsActiveWorkoutDayForStudentAsync(
                    workoutDayId,
                    studentId,
                    gymId);

        if (!validWorkoutDay)
        {
            throw new ArgumentException(
                "Dia de treino não encontrado no treino ativo do aluno.");
        }

        var execution = new WorkoutExecution
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = workoutDayId,
            CompletedAt = DateTime.UtcNow
        };

        await _workoutExecutionRepository
            .AddAsync(execution);

        await _workoutExecutionRepository
            .SaveChangesAsync();

        return new WorkoutExecutionResponse
        {
            Id = execution.Id,
            WorkoutDayId = execution.WorkoutDayId,
            CompletedAt = execution.CompletedAt
        };
    }

    public async Task<List<WorkoutHistoryItemResponse>>
    GetHistoryByStudentAsync(
        Guid gymId,
        Guid studentId,
        int page = 1,
        int pageSize = 20)
    {
        if (page <= 0)
        {
            throw new ArgumentException(
                "A página deve ser maior que zero.");
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            throw new ArgumentException(
                "O tamanho da página deve estar entre 1 e 100.");
        }

        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        var skip = (page - 1) * pageSize;

        var executions =
            await _workoutExecutionRepository
                .GetHistoryByStudentAsync(
                    studentId,
                    gymId,
                    skip,
                    pageSize);

        return executions
            .Select(x => new WorkoutHistoryItemResponse
            {
                ExecutionId = x.Id,

                WorkoutId =
                    x.WorkoutDay.WorkoutId,

                WorkoutName =
                    x.WorkoutDay.Workout.Name,

                WorkoutDayId =
                    x.WorkoutDayId,

                WorkoutDayName =
                    x.WorkoutDay.Name,

                CompletedAt =
                    x.CompletedAt
            })
            .ToList();
    }

    public async Task<WorkoutDetailResponse?> GetActiveForUserAsync(
    Guid gymId,
    Guid userId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        return await GetActiveByStudentAsync(
            gymId,
            student.Id);
    }

    public async Task<WorkoutExecutionResponse> CompleteDayForUserAsync(
    Guid gymId,
    Guid userId,
    Guid workoutDayId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        return await CompleteDayAsync(
            gymId,
            student.Id,
            workoutDayId);
    }

    public async Task<List<WorkoutHistoryItemResponse>>
    GetHistoryForUserAsync(
        Guid gymId,
        Guid userId,
        int page = 1,
        int pageSize = 20)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        return await GetHistoryByStudentAsync(
            gymId,
            student.Id,
            page,
            pageSize);
    }

    public async Task<WorkoutResponse> UpdateAsync(
    Guid gymId,
    Guid workoutId,
    UpdateWorkoutRequest request)
    {
        var workout = await _workoutRepository
            .GetForUpdateAsync(workoutId, gymId);

        if (workout is null)
            throw new ArgumentException("Treino não encontrado.");

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome do treino é obrigatório.");

        if (request.Days.Count == 0)
            throw new ArgumentException(
                "O treino deve possuir pelo menos um dia.");

        ValidateUpdateDays(request.Days);

        var exerciseIds = request.Days
            .SelectMany(x => x.Exercises)
            .Select(x => x.ExerciseId)
            .Distinct()
            .ToList();

        var exercises = await _exerciseRepository
            .GetByIdsAsync(exerciseIds, gymId);

        var exercisesById = exercises
            .ToDictionary(x => x.Id);

        foreach (var exerciseId in exerciseIds)
        {
            if (!exercisesById.TryGetValue(
                    exerciseId,
                    out var exercise))
            {
                throw new ArgumentException(
                    $"Exercício '{exerciseId}' não encontrado.");
            }

            if (!exercise.IsActive)
            {
                throw new InvalidOperationException(
                    $"O exercício '{exercise.Name}' está inativo.");
            }
        }

        var existingDaysById = workout.Days
            .ToDictionary(x => x.Id);

        ValidateExecutedDaysCannotChange(
            workout,
            request);

        var requestedExistingDayIds = request.Days
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var daysToRemove = workout.Days
            .Where(x => !requestedExistingDayIds.Contains(x.Id))
            .ToList();

        foreach (var day in daysToRemove)
        {
            if (day.Executions.Count > 0)
            {
                throw new InvalidOperationException(
                    $"O dia '{day.Name}' possui histórico de execução e não pode ser removido.");
            }

            workout.Days.Remove(day);
        }

        foreach (var dayRequest in request.Days)
        {
            WorkoutDay day;

            if (dayRequest.Id.HasValue)
            {
                if (!existingDaysById.TryGetValue(
                        dayRequest.Id.Value,
                        out day!))
                {
                    throw new ArgumentException(
                        $"Dia '{dayRequest.Id}' não pertence a este treino.");
                }

                day.Name = dayRequest.Name.Trim();
                day.Order = dayRequest.Order;
            }
            else
            {
                day = new WorkoutDay
                {
                    Id = Guid.NewGuid(),
                    WorkoutId = workout.Id,
                    Name = dayRequest.Name.Trim(),
                    Order = dayRequest.Order
                };

                workout.Days.Add(day);
            }

            UpdateExercises(
                day,
                dayRequest.Exercises);
        }

        workout.Name = name;
        workout.Description =
            request.Description?.Trim();
        workout.UpdatedAt = DateTime.UtcNow;

        await _workoutRepository.SaveChangesAsync();

        return new WorkoutResponse
        {
            Id = workout.Id,
            StudentId = workout.StudentId,
            SourceWorkoutTemplateId =
                workout.SourceWorkoutTemplateId,
            Name = workout.Name,
            Description = workout.Description,
            IsActive = workout.IsActive,
            CreatedAt = workout.CreatedAt
        };
    }

    public async Task<WorkoutDetailResponse?> GetActiveByStudentAsync(
    Guid gymId,
    Guid studentId)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            throw new ArgumentException("Aluno não encontrado.");

        var workout = await _workoutRepository
            .GetActiveByStudentAsync(studentId, gymId);

        if (workout is null)
            return null;

        return MapToDetailResponse(workout);
    }

    private static WorkoutDetailResponse MapToDetailResponse(
    Workout workout)
    {
        return new WorkoutDetailResponse
        {
            Id = workout.Id,
            StudentId = workout.StudentId,
            SourceWorkoutTemplateId =
                workout.SourceWorkoutTemplateId,
            Name = workout.Name,
            Description = workout.Description,
            IsActive = workout.IsActive,
            CreatedAt = workout.CreatedAt,
            UpdatedAt = workout.UpdatedAt,

            Days = workout.Days
                .OrderBy(x => x.Order)
                .Select(day => new WorkoutDayResponse
                {
                    Id = day.Id,
                    Name = day.Name,
                    Order = day.Order,

                    Exercises = day.Exercises
                        .OrderBy(x => x.Order)
                        .Select(exercise =>
                            new WorkoutExerciseResponse
                            {
                                Id = exercise.Id,
                                ExerciseId =
                                    exercise.ExerciseId,

                                ExerciseName =
                                    exercise.Exercise.Name,

                                MuscleGroup =
                                    exercise.Exercise.MuscleGroup,

                                Sets = exercise.Sets,

                                Repetitions =
                                    exercise.Repetitions,

                                RestSeconds =
                                    exercise.RestSeconds,

                                Notes =
                                    exercise.Notes,

                                Order =
                                    exercise.Order
                            })
                        .ToList()
                })
                .ToList()
        };
    }

    private static void UpdateExercises(
    WorkoutDay day,
    List<UpdateWorkoutExerciseRequest> requests)
    {
        var existingById = day.Exercises
            .ToDictionary(x => x.Id);

        var requestedExistingIds = requests
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var toRemove = day.Exercises
            .Where(x => !requestedExistingIds.Contains(x.Id))
            .ToList();

        foreach (var exercise in toRemove)
        {
            day.Exercises.Remove(exercise);
        }

        foreach (var request in requests)
        {
            WorkoutExercise workoutExercise;

            if (request.Id.HasValue)
            {
                if (!existingById.TryGetValue(
                        request.Id.Value,
                        out workoutExercise!))
                {
                    throw new ArgumentException(
                        $"Exercício de treino '{request.Id}' não pertence ao dia '{day.Name}'.");
                }

                workoutExercise.ExerciseId =
                    request.ExerciseId;
                workoutExercise.Sets =
                    request.Sets;
                workoutExercise.Repetitions =
                    request.Repetitions.Trim();
                workoutExercise.RestSeconds =
                    request.RestSeconds;
                workoutExercise.Notes =
                    request.Notes?.Trim();
                workoutExercise.Order =
                    request.Order;
            }
            else
            {
                day.Exercises.Add(
                    new WorkoutExercise
                    {
                        Id = Guid.NewGuid(),
                        WorkoutDayId = day.Id,
                        ExerciseId = request.ExerciseId,
                        Sets = request.Sets,
                        Repetitions =
                            request.Repetitions.Trim(),
                        RestSeconds =
                            request.RestSeconds,
                        Notes =
                            request.Notes?.Trim(),
                        Order =
                            request.Order
                    });
            }
        }
    }

    private static void ValidateUpdateDays(
    List<UpdateWorkoutDayRequest> days)
    {
        if (days.Any(x =>
                string.IsNullOrWhiteSpace(x.Name)))
        {
            throw new ArgumentException(
                "Todos os dias devem possuir um nome.");
        }

        if (days
            .GroupBy(x => x.Order)
            .Any(x => x.Count() > 1))
        {
            throw new ArgumentException(
                "A ordem dos dias não pode se repetir.");
        }

        var repeatedDayIds = days
            .Where(x => x.Id.HasValue)
            .GroupBy(x => x.Id!.Value)
            .Any(x => x.Count() > 1);

        if (repeatedDayIds)
        {
            throw new ArgumentException(
                "Um mesmo dia não pode aparecer mais de uma vez.");
        }

        foreach (var day in days)
        {
            if (day.Exercises.Count == 0)
            {
                throw new ArgumentException(
                    $"O dia '{day.Name}' deve possuir pelo menos um exercício.");
            }

            if (day.Exercises.Any(x => x.Sets <= 0))
            {
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir pelo menos uma série.");
            }

            if (day.Exercises.Any(x =>
                    string.IsNullOrWhiteSpace(
                        x.Repetitions)))
            {
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir repetições.");
            }

            if (day.Exercises.Any(x =>
                    x.RestSeconds.HasValue &&
                    x.RestSeconds.Value < 0))
            {
                throw new ArgumentException(
                    $"O descanso no dia '{day.Name}' não pode ser negativo.");
            }

            if (day.Exercises
                .GroupBy(x => x.Order)
                .Any(x => x.Count() > 1))
            {
                throw new ArgumentException(
                    $"A ordem dos exercícios no dia '{day.Name}' não pode se repetir.");
            }

            if (day.Exercises
                .Where(x => x.Id.HasValue)
                .GroupBy(x => x.Id!.Value)
                .Any(x => x.Count() > 1))
            {
                throw new ArgumentException(
                    $"Um mesmo exercício do dia '{day.Name}' não pode aparecer mais de uma vez.");
            }
        }
    }

    private static void ValidateExecutedDaysCannotChange(
    Workout workout,
    UpdateWorkoutRequest request)
    {
        var requestedDaysById = request.Days
            .Where(x => x.Id.HasValue)
            .ToDictionary(x => x.Id!.Value);

        foreach (var existingDay in
                 workout.Days.Where(x => x.Executions.Count > 0))
        {
            if (!requestedDaysById.TryGetValue(
                    existingDay.Id,
                    out var requestedDay))
            {
                throw new InvalidOperationException(
                    $"O dia '{existingDay.Name}' já possui execuções e não pode ser removido.");
            }

            if (existingDay.Name != requestedDay.Name.Trim() ||
                existingDay.Order != requestedDay.Order)
            {
                throw new InvalidOperationException(
                    $"O dia '{existingDay.Name}' já possui execuções e não pode ter nome ou ordem alterados.");
            }

            var existingExercises = existingDay.Exercises
                .OrderBy(x => x.Order)
                .ToList();

            var requestedExercises =
                requestedDay.Exercises
                    .OrderBy(x => x.Order)
                    .ToList();

            if (existingExercises.Count !=
                requestedExercises.Count)
            {
                throw new InvalidOperationException(
                    $"O dia '{existingDay.Name}' já possui execuções e não pode ter seus exercícios alterados.");
            }

            for (var i = 0;
                 i < existingExercises.Count;
                 i++)
            {
                var existing = existingExercises[i];
                var requested = requestedExercises[i];

                if (requested.Id != existing.Id ||
                    requested.ExerciseId != existing.ExerciseId ||
                    requested.Sets != existing.Sets ||
                    requested.Repetitions.Trim() != existing.Repetitions ||
                    requested.RestSeconds != existing.RestSeconds ||
                    requested.Notes?.Trim() != existing.Notes ||
                    requested.Order != existing.Order)
                {
                    throw new InvalidOperationException(
                        $"O dia '{existingDay.Name}' já possui execuções e não pode ter sua prescrição alterada.");
                }
            }
        }
    }

    private readonly IWorkoutExecutionRepository
    _workoutExecutionRepository;

    private static void ValidateDays(
        List<CreateWorkoutDayRequest> days)
    {
        if (days.Any(x =>
                string.IsNullOrWhiteSpace(x.Name)))
        {
            throw new ArgumentException(
                "Todos os dias devem possuir um nome.");
        }

        if (days
            .GroupBy(x => x.Order)
            .Any(x => x.Count() > 1))
        {
            throw new ArgumentException(
                "A ordem dos dias não pode se repetir.");
        }

        foreach (var day in days)
        {
            if (day.Exercises.Count == 0)
            {
                throw new ArgumentException(
                    $"O dia '{day.Name}' deve possuir pelo menos um exercício.");
            }

            if (day.Exercises.Any(x => x.Sets <= 0))
            {
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir pelo menos uma série.");
            }

            if (day.Exercises.Any(x =>
                    string.IsNullOrWhiteSpace(
                        x.Repetitions)))
            {
                throw new ArgumentException(
                    $"Todos os exercícios do dia '{day.Name}' devem possuir repetições.");
            }

            if (day.Exercises.Any(x =>
                    x.RestSeconds.HasValue &&
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
}