using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.Workouts;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class WorkoutService
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IWorkoutTemplateRepository _workoutTemplateRepository;

    private readonly IGymTimeZoneProvider
    _gymTimeZoneProvider;
    private readonly IWorkoutDayProgressRepository _workoutDayProgressRepository;

    public WorkoutService(
    IWorkoutRepository workoutRepository,
    IStudentRepository studentRepository,
    IExerciseRepository exerciseRepository,
    IWorkoutTemplateRepository workoutTemplateRepository,
    IWorkoutExecutionRepository workoutExecutionRepository,
    IWorkoutDayProgressRepository workoutDayProgressRepository,
    IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _workoutRepository = workoutRepository;
        _studentRepository = studentRepository;
        _exerciseRepository = exerciseRepository;
        _workoutTemplateRepository = workoutTemplateRepository;
        _workoutExecutionRepository = workoutExecutionRepository;
        _workoutDayProgressRepository = workoutDayProgressRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
    }

    public async Task<WorkoutResponse> CreateManualAsync(
        Guid gymId,
        CreateWorkoutRequest request)
    {
        var student = await _studentRepository
    .GetByIdAndGymIdAsync(request.StudentId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        if (!student.User.IsActive)
            throw new InvalidOperationException(
                "Não é possível atribuir treino a um aluno inativo.");

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome do treino é obrigatório.");

        PersistenceTextPolicy.ValidateMaxLength(
             name,
            PersistenceTextPolicy.WorkoutNameMaxLength,
            "O nome do treino");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Description,
            PersistenceTextPolicy.DescriptionMaxLength,
            "A descrição");

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
            throw new KeyNotFoundException("Aluno não encontrado.");

        if (!student.User.IsActive)
        {
            throw new InvalidOperationException(
                "Não é possível atribuir treino a um aluno inativo.");
        }

        var template = await _workoutTemplateRepository
            .GetByIdAsync(request.TemplateId, gymId);

        if (template is null)
            throw new KeyNotFoundException("Modelo de treino não encontrado.");

        if (!template.IsActive)
        {
            throw new InvalidOperationException(
                "Não é possível utilizar um modelo de treino inativo.");
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "O nome do treino é obrigatório.");
        }

        PersistenceTextPolicy.ValidateMaxLength(
            name,
            PersistenceTextPolicy.WorkoutNameMaxLength,
            "O nome do treino");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Description,
            PersistenceTextPolicy.DescriptionMaxLength,
            "A descrição");

        if (request.Days.Count == 0)
        {
            throw new ArgumentException(
                "O treino deve possuir pelo menos um dia.");
        }

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

            SourceWorkoutTemplateId = template.Id,

            Name = name,
            Description = request.Description?.Trim(),

            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var dayRequest in
                 request.Days.OrderBy(x => x.Order))
        {
            var workoutDay = new WorkoutDay
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

                workoutDay.Exercises.Add(
                    new WorkoutExercise
                    {
                        Id = Guid.NewGuid(),
                        WorkoutDayId = workoutDay.Id,
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
            throw new KeyNotFoundException(
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
            throw new KeyNotFoundException(
                "Dia de treino não encontrado no treino ativo do aluno.");
        }

        var progress = await _workoutDayProgressRepository
    .GetByStudentAsync(studentId, gymId);

        if (progress is null ||
            progress.WorkoutDayId != workoutDayId)
        {
            throw new InvalidOperationException(
                "Conclua todos os exercícios antes de finalizar o dia.");
        }

        var totalExercises =
            await _workoutDayProgressRepository
                .CountExercisesInDayAsync(
                    workoutDayId,
                    studentId,
                    gymId);

        if (totalExercises == 0 ||
            progress.CompletedExercises.Count != totalExercises)
        {
            throw new InvalidOperationException(
                "Conclua todos os exercícios antes de finalizar o dia.");
        }

        var nowUtc = DateTime.UtcNow;

        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var executionDate = GetGymDate(
            timeZone,
            nowUtc);

        var alreadyCompleted =
            await _workoutExecutionRepository
                .ExistsForWorkoutDayOnDateAsync(
                    workoutDayId,
                    executionDate);

        if (alreadyCompleted)
        {
            throw new InvalidOperationException(
                "Este treino já foi concluído hoje.");
        }

        var execution = new WorkoutExecution
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = workoutDayId,
            ExecutionDate = executionDate,
            CompletedAt = nowUtc
        };

        await _workoutExecutionRepository
            .AddAsync(execution);

        _workoutDayProgressRepository
            .RemoveProgress(progress);

        await _workoutExecutionRepository
            .SaveChangesAsync();

        return new WorkoutExecutionResponse
        {
            Id = execution.Id,
            WorkoutDayId = execution.WorkoutDayId,
            CompletedAt = execution.CompletedAt,
            CompletedAtUtcOffsetMinutes =
            GetGymUtcOffsetMinutes(
                timeZone,
                execution.CompletedAt)
        };
    }

    public async Task<WorkoutExerciseCompletionResponse> UncompleteExerciseAsync(
    Guid gymId,
    Guid studentId,
    Guid workoutDayId,
    Guid workoutExerciseId)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        if (!student.User.IsActive)
        {
            throw new InvalidOperationException(
                "Aluno inativo não pode alterar progresso de treino.");
        }

        var validExercise =
            await _workoutDayProgressRepository
                .IsWorkoutExerciseInActiveDayAsync(
                    workoutExerciseId,
                    workoutDayId,
                    studentId,
                    gymId);

        if (!validExercise)
        {
            throw new KeyNotFoundException(
                "Exercício não encontrado neste dia do treino ativo.");
        }

        var progress = await _workoutDayProgressRepository
            .GetByStudentAsync(studentId, gymId);

        if (progress is null ||
            progress.WorkoutDayId != workoutDayId)
        {
            throw new InvalidOperationException(
                "Este dia não possui progresso em andamento.");
        }

        var completion = progress.CompletedExercises
            .FirstOrDefault(x =>
                x.WorkoutExerciseId == workoutExerciseId);

        if (completion is not null)
        {
            _workoutDayProgressRepository
                .RemoveCompletion(completion);

            progress.CompletedExercises.Remove(completion);

            if (progress.CompletedExercises.Count == 0)
            {
                _workoutDayProgressRepository
                    .RemoveProgress(progress);
            }
            else
            {
                progress.UpdatedAt = DateTime.UtcNow;
            }

            await _workoutDayProgressRepository
                .SaveChangesAsync();
        }

        var totalExercises =
            await _workoutDayProgressRepository
                .CountExercisesInDayAsync(
                    workoutDayId,
                    studentId,
                    gymId);

        return new WorkoutExerciseCompletionResponse
        {
            WorkoutDayId = workoutDayId,
            WorkoutExerciseId = workoutExerciseId,
            IsCompleted = false,
            CompletedExercises = progress.CompletedExercises.Count,
            TotalExercises = totalExercises
        };
    }

    public async Task<WorkoutExerciseCompletionResponse> CompleteExerciseAsync(
    Guid gymId,
    Guid studentId,
    Guid workoutDayId,
    Guid workoutExerciseId)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        if (!student.User.IsActive)
        {
            throw new InvalidOperationException(
                "Aluno inativo não pode registrar progresso de treino.");
        }

        var validExercise =
            await _workoutDayProgressRepository
                .IsWorkoutExerciseInActiveDayAsync(
                    workoutExerciseId,
                    workoutDayId,
                    studentId,
                    gymId);

        if (!validExercise)
        {
            throw new KeyNotFoundException(
                "Exercício não encontrado neste dia do treino ativo.");
        }

        var progress = await _workoutDayProgressRepository
            .GetByStudentAsync(studentId, gymId);

        if (progress is not null)
        {
            // Progresso antigo de um treino que foi substituído.
            if (!progress.WorkoutDay.Workout.IsActive)
            {
                _workoutDayProgressRepository
                    .RemoveProgress(progress);

                await _workoutDayProgressRepository
                    .SaveChangesAsync();

                progress = null;
            }
            else if (progress.WorkoutDayId != workoutDayId)
            {
                throw new InvalidOperationException(
                    "Você já possui outro dia de treino em andamento.");
            }
        }

        var nowUtc = DateTime.UtcNow;

        if (progress is null)
        {
            progress = new WorkoutDayProgress
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                WorkoutDayId = workoutDayId,
                StartedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            await _workoutDayProgressRepository
                .AddProgressAsync(progress);
        }

        var existingCompletion = progress.CompletedExercises
            .FirstOrDefault(x =>
                x.WorkoutExerciseId == workoutExerciseId);

        // Deixamos a operação idempotente:
        // tocar novamente em "concluir" não duplica o registro.
        if (existingCompletion is null)
        {
            var completion = new WorkoutExerciseCompletion
            {
                Id = Guid.NewGuid(),
                WorkoutDayProgressId = progress.Id,
                WorkoutExerciseId = workoutExerciseId,
                CompletedAt = nowUtc
            };

            await _workoutDayProgressRepository
                .AddCompletionAsync(completion);

            progress.CompletedExercises.Add(completion);
            progress.UpdatedAt = nowUtc;

            await _workoutDayProgressRepository
                .SaveChangesAsync();
        }

        var totalExercises =
            await _workoutDayProgressRepository
                .CountExercisesInDayAsync(
                    workoutDayId,
                    studentId,
                    gymId);

        return new WorkoutExerciseCompletionResponse
        {
            WorkoutDayId = workoutDayId,
            WorkoutExerciseId = workoutExerciseId,
            IsCompleted = true,
            CompletedExercises = progress.CompletedExercises.Count,
            TotalExercises = totalExercises
        };
    }

    public async Task<WorkoutExerciseCompletionResponse> UncompleteExerciseForUserAsync(
    Guid gymId,
    Guid userId,
    Guid workoutDayId,
    Guid workoutExerciseId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        return await UncompleteExerciseAsync(
            gymId,
            student.Id,
            workoutDayId,
            workoutExerciseId);
    }

    public async Task<PagedResponse<WorkoutHistoryItemResponse>> GetHistoryAsync(
    Guid gymId,
    Guid studentId,
    int page,
    int pageSize)
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
            throw new KeyNotFoundException("Aluno não encontrado.");

        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var skip = (page - 1) * pageSize;

        var executions = await _workoutExecutionRepository
            .GetHistoryByStudentAsync(
                studentId,
                gymId,
                skip,
                pageSize);

        var totalCount = await _workoutExecutionRepository
            .CountHistoryByStudentAsync(
                studentId,
                gymId);

        var items = executions
            .Select(execution => new WorkoutHistoryItemResponse
            {
                ExecutionId = execution.Id,
                WorkoutId = execution.WorkoutDay.Workout.Id,
                WorkoutName = execution.WorkoutDay.Workout.Name,
                WorkoutDayId = execution.WorkoutDayId,
                WorkoutDayName = execution.WorkoutDay.Name,
                CompletedAt = execution.CompletedAt,
                CompletedAtUtcOffsetMinutes =
                GetGymUtcOffsetMinutes(
                    timeZone,
                    execution.CompletedAt)
            })
            .ToList();

        return new PagedResponse<WorkoutHistoryItemResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<WorkoutDetailResponse?> GetActiveForUserAsync(
    Guid gymId,
    Guid userId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

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
            throw new KeyNotFoundException("Aluno não encontrado.");

        return await CompleteDayAsync(
            gymId,
            student.Id,
            workoutDayId);
    }

    public async Task<WorkoutExerciseCompletionResponse> CompleteExerciseForUserAsync(
    Guid gymId,
    Guid userId,
    Guid workoutDayId,
    Guid workoutExerciseId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        return await CompleteExerciseAsync(
            gymId,
            student.Id,
            workoutDayId,
            workoutExerciseId);
    }

    public async Task<PagedResponse<WorkoutHistoryItemResponse>> GetHistoryForUserAsync(
    Guid gymId,
    Guid userId,
    int page,
    int pageSize)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        return await GetHistoryAsync(
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
            throw new KeyNotFoundException("Treino não encontrado.");

        if (!workout.IsActive)
        {
            throw new InvalidOperationException(
                "Não é possível editar uma versão histórica do treino.");
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "O nome do treino é obrigatório.");
        }

        PersistenceTextPolicy.ValidateMaxLength(
            name,
            PersistenceTextPolicy.WorkoutNameMaxLength,
            "O nome do treino");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Description,
            PersistenceTextPolicy.DescriptionMaxLength,
            "A descrição");

        if (request.Days.Count == 0)
        {
            throw new ArgumentException(
                "O treino deve possuir pelo menos um dia.");
        }

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

        var hasExecutions =
                await _workoutExecutionRepository
        .ExistsForWorkoutAsync(
            workout.Id,
            gymId);

        if (hasExecutions)
        {
            var now = DateTime.UtcNow;

            workout.IsActive = false;
            workout.UpdatedAt = now;

            var newWorkout = new Workout
            {
                Id = Guid.NewGuid(),
                StudentId = workout.StudentId,
                GymId = workout.GymId,

                SourceWorkoutTemplateId =
                    workout.SourceWorkoutTemplateId,

                Name = name,
                Description = request.Description?.Trim(),

                IsActive = true,
                CreatedAt = now
            };

            foreach (var dayRequest in
                     request.Days.OrderBy(x => x.Order))
            {
                var newDay = new WorkoutDay
                {
                    Id = Guid.NewGuid(),
                    WorkoutId = newWorkout.Id,
                    Name = dayRequest.Name.Trim(),
                    Order = dayRequest.Order
                };

                foreach (var exerciseRequest in
                         dayRequest.Exercises
                             .OrderBy(x => x.Order))
                {
                    var exercise =
                        exercisesById[
                            exerciseRequest.ExerciseId];

                    newDay.Exercises.Add(
                        new WorkoutExercise
                        {
                            Id = Guid.NewGuid(),

                            WorkoutDayId = newDay.Id,

                            ExerciseId = exercise.Id,

                            Sets =
                                exerciseRequest.Sets,

                            Repetitions =
                                exerciseRequest
                                    .Repetitions
                                    .Trim(),

                            RestSeconds =
                                exerciseRequest
                                    .RestSeconds,

                            Notes =
                                exerciseRequest
                                    .Notes
                                    ?.Trim(),

                            Order =
                                exerciseRequest.Order
                        });
                }

                newWorkout.Days.Add(newDay);
            }

            await _workoutRepository
                .AddAsync(newWorkout);

            await _workoutRepository
                .SaveChangesAsync();

            return new WorkoutResponse
            {
                Id = newWorkout.Id,
                StudentId = newWorkout.StudentId,

                SourceWorkoutTemplateId =
                    newWorkout.SourceWorkoutTemplateId,

                Name = newWorkout.Name,
                Description = newWorkout.Description,
                IsActive = newWorkout.IsActive,
                CreatedAt = newWorkout.CreatedAt
            };
        }

        var existingDaysById = workout.Days
            .ToDictionary(x => x.Id);

        var requestedExistingDayIds = request.Days
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var daysToRemove = workout.Days
            .Where(
                x => !requestedExistingDayIds.Contains(
                    x.Id))
            .ToList();

        foreach (var day in daysToRemove)
        {
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
            throw new KeyNotFoundException("Aluno não encontrado.");

        var workout = await _workoutRepository
            .GetActiveByStudentAsync(studentId, gymId);

        if (workout is null)
            return null;

        var workoutDayIds = workout.Days
            .Select(x => x.Id)
            .ToList();

        var latestExecutions = await _workoutExecutionRepository
            .GetLatestByWorkoutDayIdsAsync(workoutDayIds);

        var progress = await _workoutDayProgressRepository
            .GetByStudentAsync(studentId, gymId);

        var completedExerciseIds =
            progress is not null &&
            progress.WorkoutDay.WorkoutId == workout.Id &&
            progress.WorkoutDay.Workout.IsActive
                ? progress.CompletedExercises
                    .Select(x => x.WorkoutExerciseId)
                    .ToHashSet()
                : new HashSet<Guid>();

        var timeZone =
             _gymTimeZoneProvider.GetTimeZone(gymId);

        var today = GetGymDate(
            timeZone,
            DateTime.UtcNow);

        return MapToDetailResponse(
            workout,
            latestExecutions,
            today,
            timeZone,
            completedExerciseIds);
    }

    private static WorkoutDetailResponse MapToDetailResponse(
    Workout workout,
    List<WorkoutExecution>? latestExecutions,
    DateOnly today,
    TimeZoneInfo timeZone,
    HashSet<Guid> completedExerciseIds)
    {
        var executionsByDay = latestExecutions?
            .ToDictionary(
                x => x.WorkoutDayId,
                x => x)
            ?? new Dictionary<Guid, WorkoutExecution>();

        return new WorkoutDetailResponse
        {
            Id = workout.Id,
            StudentId = workout.StudentId,
            SourceWorkoutTemplateId = workout.SourceWorkoutTemplateId,
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

                    CompletedToday =
                        executionsByDay.TryGetValue(
                            day.Id,
                            out var execution) &&
                        execution.ExecutionDate == today,

                    LastCompletedAt =
                        executionsByDay.TryGetValue(
                            day.Id,
                            out var lastExecution)
                                ? lastExecution.CompletedAt
                                : null,

                    LastCompletedAtUtcOffsetMinutes =
                        executionsByDay.TryGetValue(
                         day.Id,
                         out var offsetExecution)
                             ? GetGymUtcOffsetMinutes(
                        timeZone,
                                offsetExecution.CompletedAt)
                                : null,

                    Exercises = day.Exercises
                        .OrderBy(x => x.Order)
                        .Select(exercise => new WorkoutExerciseResponse
                        {
                            Id = exercise.Id,
                            ExerciseId = exercise.ExerciseId,
                            ExerciseName = exercise.Exercise.Name,
                            MuscleGroup = exercise.Exercise.MuscleGroup,
                            Sets = exercise.Sets,
                            Repetitions = exercise.Repetitions,
                            RestSeconds = exercise.RestSeconds,
                            Notes = exercise.Notes,
                            Order = exercise.Order,
                            IsCompleted = completedExerciseIds.Contains(exercise.Id)
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private static int GetGymUtcOffsetMinutes(
    TimeZoneInfo timeZone,
    DateTime utcDateTime)
    {
        var normalizedUtc = utcDateTime.Kind switch
        {
            DateTimeKind.Utc => utcDateTime,

            DateTimeKind.Local =>
                utcDateTime.ToUniversalTime(),

            _ => DateTime.SpecifyKind(
                utcDateTime,
                DateTimeKind.Utc)
        };

        return checked(
            (int)timeZone
                .GetUtcOffset(normalizedUtc)
                .TotalMinutes);
    }

    private static DateOnly GetGymDate(
        TimeZoneInfo timeZone,
        DateTime utcDateTime)
    {
        var normalizedUtc = utcDateTime.Kind switch
        {
            DateTimeKind.Utc => utcDateTime,

            DateTimeKind.Local =>
                utcDateTime.ToUniversalTime(),

            _ => DateTime.SpecifyKind(
                utcDateTime,
                DateTimeKind.Utc)
        };

        var localDateTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                normalizedUtc,
                timeZone);

        return DateOnly.FromDateTime(
            localDateTime);
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

        foreach (var day in days)
        {
            PersistenceTextPolicy.ValidateMaxLength(
                day.Name,
                PersistenceTextPolicy.DayNameMaxLength,
                "O nome do dia");

            foreach (var exercise in day.Exercises)
            {
                PersistenceTextPolicy.ValidateMaxLength(
                    exercise.Repetitions,
                    PersistenceTextPolicy.RepetitionsMaxLength,
                    "As repetições");

                PersistenceTextPolicy.ValidateMaxLength(
                    exercise.Notes,
                    PersistenceTextPolicy.NotesMaxLength,
                    "As observações");
            }
        }
    }
}