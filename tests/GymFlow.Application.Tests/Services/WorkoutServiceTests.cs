using GymFlow.Application.DTOs.Workouts;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class WorkoutServiceTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IWorkoutTemplateRepository _workoutTemplateRepository;
    private readonly IWorkoutExecutionRepository _workoutExecutionRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;

    private readonly WorkoutService _service;

    public WorkoutServiceTests()
    {
        _workoutRepository =
            Substitute.For<IWorkoutRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _exerciseRepository =
            Substitute.For<IExerciseRepository>();

        _workoutTemplateRepository =
            Substitute.For<IWorkoutTemplateRepository>();

        _workoutExecutionRepository =
            Substitute.For<IWorkoutExecutionRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _gymTimeZoneProvider
            .GetTimeZone(Arg.Any<Guid>())
            .Returns(TimeZoneInfo.Utc);

        _service = new WorkoutService(
            _workoutRepository,
            _studentRepository,
            _exerciseRepository,
            _workoutTemplateRepository,
            _workoutExecutionRepository,
            _gymTimeZoneProvider);
    }

    [Fact]
    public async Task CreateManualAsync_WithValidData_ShouldCreateWorkoutAndDeactivateCurrentWorkout()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var exercise1 =
            CreateExercise(
                gymId,
                "Supino Reto",
                "Peitoral",
                true);

        var exercise2 =
            CreateExercise(
                gymId,
                "Tríceps Corda",
                "Tríceps",
                true);

        var currentWorkout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            GymId = gymId,
            Name = "Treino Antigo",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var request = new CreateWorkoutRequest
        {
            StudentId = student.Id,
            Name = "  Treino Novo  ",
            Description = "  Peito e tríceps  ",
            Days =
            [
                new CreateWorkoutDayRequest
                {
                    Name = "  Dia A  ",
                    Order = 1,
                    Exercises =
                    [
                        new CreateWorkoutExerciseRequest
                        {
                            ExerciseId = exercise2.Id,
                            Sets = 3,
                            Repetitions = "  12  ",
                            RestSeconds = 60,
                            Notes = "  Controlado  ",
                            Order = 2
                        },
                        new CreateWorkoutExerciseRequest
                        {
                            ExerciseId = exercise1.Id,
                            Sets = 4,
                            Repetitions = "  8-10  ",
                            RestSeconds = 90,
                            Order = 1
                        }
                    ]
                }
            ]
        };

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                    exercise1,
                    exercise2
                });

        _workoutRepository
            .GetActiveForUpdateAsync(
                student.Id,
                gymId)
            .Returns(currentWorkout);

        var result =
            await _service.CreateManualAsync(
                gymId,
                request);

        Assert.NotEqual(Guid.Empty, result.Id);

        Assert.Equal(
            student.Id,
            result.StudentId);

        Assert.Null(
            result.SourceWorkoutTemplateId);

        Assert.Equal(
            "Treino Novo",
            result.Name);

        Assert.Equal(
            "Peito e tríceps",
            result.Description);

        Assert.True(result.IsActive);

        Assert.False(currentWorkout.IsActive);
        Assert.NotNull(currentWorkout.UpdatedAt);

        await _workoutRepository
            .Received(1)
            .AddAsync(
                Arg.Is<Workout>(workout =>
                    workout.GymId == gymId &&
                    workout.StudentId == student.Id &&
                    workout.SourceWorkoutTemplateId == null &&
                    workout.Name == "Treino Novo" &&
                    workout.Description == "Peito e tríceps" &&
                    workout.IsActive &&
                    workout.Days.Count == 1 &&
                    workout.Days.Single().Name == "Dia A" &&
                    workout.Days.Single().Exercises.Count == 2 &&
                    workout.Days.Single().Exercises
                        .OrderBy(x => x.Order)
                        .First().ExerciseId == exercise1.Id &&
                    workout.Days.Single().Exercises
                        .OrderBy(x => x.Order)
                        .Last().ExerciseId == exercise2.Id));

        await _workoutRepository
            .Received(1)
            .GetActiveForUpdateAsync(
                student.Id,
                gymId);

        await _workoutRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CreateManualAsync_WhenStudentDoesNotExistInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();

        var request =
            CreateValidManualRequest(
                Guid.NewGuid(),
                Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                request.StudentId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WhenStudentIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, false);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateManualAsync_WithInvalidName_ShouldThrowArgumentException(
        string name)
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Name = name;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WithoutDays_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var request = new CreateWorkoutRequest
        {
            StudentId = student.Id,
            Name = "Treino A",
            Days = []
        };

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WhenExerciseIsNotFoundInGym_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var exerciseId =
            Guid.NewGuid();

        var request =
            CreateValidManualRequest(
                student.Id,
                exerciseId);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(new List<Exercise>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _exerciseRepository
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IEnumerable<Guid>>(
                    ids => ids.Contains(exerciseId)),
                gymId);

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WhenExerciseIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var exercise =
            CreateExercise(
                gymId,
                "Supino",
                "Peitoral",
                false);

        var request =
            CreateValidManualRequest(
                student.Id,
                exercise.Id);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                    exercise
                });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .GetActiveForUpdateAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WithDayWithoutName_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Days[0].Name = "   ";

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WithDuplicateDayOrder_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var exerciseId = Guid.NewGuid();

        var request =
            CreateValidManualRequest(
                student.Id,
                exerciseId);

        request.Days.Add(
            new CreateWorkoutDayRequest
            {
                Name = "Dia B",
                Order = 1,
                Exercises =
                [
                    new CreateWorkoutExerciseRequest
                {
                    ExerciseId = exerciseId,
                    Sets = 3,
                    Repetitions = "10",
                    RestSeconds = 60,
                    Order = 1
                }
                ]
            });

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WithDayWithoutExercises_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Days[0].Exercises.Clear();

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateManualAsync_WithInvalidSets_ShouldThrowArgumentException(
    int sets)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .Sets = sets;

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateManualAsync_WithInvalidRepetitions_ShouldThrowArgumentException(
    string repetitions)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .Repetitions = repetitions;

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WithNegativeRestSeconds_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .RestSeconds = -1;

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateManualAsync_WithDuplicateExerciseOrder_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var request =
            CreateValidManualRequest(
                student.Id,
                Guid.NewGuid());

        request.Days[0]
            .Exercises.Add(
                new CreateWorkoutExerciseRequest
                {
                    ExerciseId = Guid.NewGuid(),
                    Sets = 3,
                    Repetitions = "12",
                    RestSeconds = 60,
                    Order = 1
                });

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithValidData_ShouldCreateWorkoutLinkedToTemplate()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var template =
            CreateWorkoutTemplate(
                gymId,
                "Modelo A",
                true);

        var exercise =
            CreateExercise(
                gymId,
                "Supino Reto",
                "Peitoral",
                true);

        var currentWorkout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            GymId = gymId,
            Name = "Treino Antigo",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var request =
            CreateValidTemplateRequest(
                student.Id,
                template.Id,
                exercise.Id);

        request.Name = "  Treino do Modelo  ";
        request.Description = "  Personalizado  ";

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        _workoutRepository
            .GetActiveForUpdateAsync(
                student.Id,
                gymId)
            .Returns(currentWorkout);

        var result =
            await _service.CreateFromTemplateAsync(
                gymId,
                request);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(student.Id, result.StudentId);
        Assert.Equal(
            template.Id,
            result.SourceWorkoutTemplateId);

        Assert.Equal(
            "Treino do Modelo",
            result.Name);

        Assert.Equal(
            "Personalizado",
            result.Description);

        Assert.True(result.IsActive);

        Assert.False(currentWorkout.IsActive);
        Assert.NotNull(currentWorkout.UpdatedAt);

        await _workoutTemplateRepository
            .Received(1)
            .GetByIdAsync(
                template.Id,
                gymId);

        await _workoutRepository
            .Received(1)
            .AddAsync(
                Arg.Is<Workout>(workout =>
                    workout.GymId == gymId &&
                    workout.StudentId == student.Id &&
                    workout.SourceWorkoutTemplateId == template.Id &&
                    workout.Name == "Treino do Modelo" &&
                    workout.Description == "Personalizado" &&
                    workout.IsActive &&
                    workout.Days.Count == 1 &&
                    workout.Days.Single().Exercises.Count == 1 &&
                    workout.Days.Single().Exercises.Single().ExerciseId ==
                        exercise.Id));

        await _workoutRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WhenStudentDoesNotExistInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();

        var request =
            CreateValidTemplateRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                request.StudentId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WhenStudentIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, false);

        var request =
            CreateValidTemplateRequest(
                student.Id,
                Guid.NewGuid(),
                Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WhenTemplateIsNotFoundInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var templateId = Guid.NewGuid();

        var request =
            CreateValidTemplateRequest(
                student.Id,
                templateId,
                Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                templateId,
                gymId)
            .Returns((WorkoutTemplate?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _workoutTemplateRepository
            .Received(1)
            .GetByIdAsync(
                templateId,
                gymId);

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WhenTemplateIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var template =
            CreateWorkoutTemplate(
                gymId,
                "Modelo Inativo",
                false);

        var request =
            CreateValidTemplateRequest(
                student.Id,
                template.Id,
                Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFromTemplateAsync_WithInvalidName_ShouldThrowArgumentException(
    string name)
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var template =
            CreateWorkoutTemplate(
                gymId,
                "Modelo A",
                true);

        var request =
            CreateValidTemplateRequest(
                student.Id,
                template.Id,
                Guid.NewGuid());

        request.Name = name;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithoutDays_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var template =
            CreateWorkoutTemplate(
                gymId,
                "Modelo A",
                true);

        var request = new CreateWorkoutFromTemplateRequest
        {
            StudentId = student.Id,
            TemplateId = template.Id,
            Name = "Treino A",
            Days = []
        };

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WhenExerciseIsNotFoundInGym_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var template =
            CreateWorkoutTemplate(
                gymId,
                "Modelo A",
                true);

        var exerciseId = Guid.NewGuid();

        var request =
            CreateValidTemplateRequest(
                student.Id,
                template.Id,
                exerciseId);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(new List<Exercise>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _exerciseRepository
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IEnumerable<Guid>>(
                    ids => ids.Contains(exerciseId)),
                gymId);

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WhenExerciseIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(gymId, true);

        var template =
            CreateWorkoutTemplate(
                gymId,
                "Modelo A",
                true);

        var exercise =
            CreateExercise(
                gymId,
                "Supino",
                "Peitoral",
                false);

        var request =
            CreateValidTemplateRequest(
                student.Id,
                template.Id,
                exercise.Id);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateFromTemplateAsync(
                gymId,
                request));

        await _workoutRepository
            .DidNotReceive()
            .GetActiveForUpdateAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());
    }

    [Fact]
    public async Task CompleteDayAsync_WithValidData_ShouldCreateExecution()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);
        var workoutDayId = Guid.NewGuid();

        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "GymFlowTestZone",
            TimeSpan.FromHours(-3),
            "GymFlow Test Zone",
            "GymFlow Test Zone");

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(timeZone);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId)
            .Returns(true);

        _workoutExecutionRepository
            .ExistsForWorkoutDayOnDateAsync(
                workoutDayId,
                Arg.Any<DateOnly>())
            .Returns(false);

        WorkoutExecution? capturedExecution = null;

        _workoutExecutionRepository
            .When(x => x.AddAsync(
                Arg.Any<WorkoutExecution>()))
            .Do(call =>
                capturedExecution =
                    call.Arg<WorkoutExecution>());

        var result = await _service.CompleteDayAsync(
            gymId,
            student.Id,
            workoutDayId);

        Assert.NotNull(capturedExecution);

        Assert.NotEqual(
            Guid.Empty,
            capturedExecution.Id);

        Assert.Equal(
            workoutDayId,
            capturedExecution.WorkoutDayId);

        var expectedExecutionDate =
            DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    capturedExecution.CompletedAt,
                    timeZone));

        Assert.Equal(
            expectedExecutionDate,
            capturedExecution.ExecutionDate);

        Assert.Equal(
            capturedExecution.Id,
            result.Id);

        Assert.Equal(
            workoutDayId,
            result.WorkoutDayId);

        Assert.Equal(
            capturedExecution.CompletedAt,
            result.CompletedAt);

        Assert.Equal(
             -180,
            result.CompletedAtUtcOffsetMinutes);

        _gymTimeZoneProvider
            .Received(1)
            .GetTimeZone(gymId);

        await _workoutExecutionRepository
            .Received(1)
            .ExistsForWorkoutDayOnDateAsync(
                workoutDayId,
                expectedExecutionDate);

        await _workoutExecutionRepository
            .Received(1)
            .AddAsync(capturedExecution);

        await _workoutExecutionRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CompleteDayAsync_WhenStudentDoesNotExistInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var workoutDayId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CompleteDayAsync(
                gymId,
                studentId,
                workoutDayId));

        await _workoutExecutionRepository
            .DidNotReceive()
            .IsActiveWorkoutDayForStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>());

        await _workoutExecutionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<WorkoutExecution>());
    }

    [Fact]
    public async Task CompleteDayAsync_WhenStudentIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, false);
        var workoutDayId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CompleteDayAsync(
                gymId,
                student.Id,
                workoutDayId));

        await _workoutExecutionRepository
            .DidNotReceive()
            .IsActiveWorkoutDayForStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>());

        await _workoutExecutionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<WorkoutExecution>());
    }

    [Fact]
    public async Task CompleteDayAsync_WhenWorkoutDayIsNotInStudentsActiveWorkout_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);
        var workoutDayId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId)
            .Returns(false);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CompleteDayAsync(
                gymId,
                student.Id,
                workoutDayId));

        await _workoutExecutionRepository
            .Received(1)
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId);

        _gymTimeZoneProvider
            .DidNotReceive()
            .GetTimeZone(Arg.Any<Guid>());

        await _workoutExecutionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<WorkoutExecution>());
    }

    [Fact]
    public async Task CompleteDayAsync_WhenAlreadyCompletedToday_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);
        var workoutDayId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId)
            .Returns(true);

        _workoutExecutionRepository
            .ExistsForWorkoutDayOnDateAsync(
                workoutDayId,
                Arg.Any<DateOnly>())
            .Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CompleteDayAsync(
                gymId,
                student.Id,
                workoutDayId));

        await _workoutExecutionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<WorkoutExecution>());

        await _workoutExecutionRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CompleteDayAsync_ShouldValidateWorkoutDayUsingStudentAndGym()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);
        var workoutDayId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId)
            .Returns(false);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CompleteDayAsync(
                gymId,
                student.Id,
                workoutDayId));

        await _studentRepository
            .Received(1)
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId);

        await _workoutExecutionRepository
            .Received(1)
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId);
    }

    [Fact]
    public async Task GetHistoryAsync_WithValidParameters_ShouldReturnPagedHistory()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "GymFlowHistoryTestZone",
             TimeSpan.FromHours(-3),
            "GymFlow History Test Zone",
            "GymFlow History Test Zone");

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(timeZone);

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            GymId = gymId,
            Name = "Treino A",
            IsActive = true
        };

        var day = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia A",
            Order = 1
        };

        var execution1 = new WorkoutExecution
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = day.Id,
            WorkoutDay = day,
            ExecutionDate = new DateOnly(2026, 8, 26),
            CompletedAt = new DateTime(
                2026, 8, 26, 12, 0, 0,
                DateTimeKind.Utc)
        };

        var execution2 = new WorkoutExecution
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = day.Id,
            WorkoutDay = day,
            ExecutionDate = new DateOnly(2026, 8, 25),
            CompletedAt = new DateTime(
                2026, 8, 25, 12, 0, 0,
                DateTimeKind.Utc)
        };

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .GetHistoryByStudentAsync(
                student.Id,
                gymId,
                20,
                20)
            .Returns(
                new List<WorkoutExecution>
                {
                execution1,
                execution2
                });

        _workoutExecutionRepository
            .CountHistoryByStudentAsync(
                student.Id,
                gymId)
            .Returns(25);

        var result = await _service.GetHistoryAsync(
            gymId,
            student.Id,
            page: 2,
            pageSize: 20);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(
            execution1.Id,
            result.Items[0].ExecutionId);

        Assert.Equal(
            workout.Id,
            result.Items[0].WorkoutId);

        Assert.Equal(
            "Treino A",
            result.Items[0].WorkoutName);

        Assert.Equal(
            day.Id,
            result.Items[0].WorkoutDayId);

        Assert.Equal(
            "Dia A",
            result.Items[0].WorkoutDayName);

        Assert.Equal(
            execution1.CompletedAt,
            result.Items[0].CompletedAt);

        Assert.Equal(
             -180,
             result.Items[0].CompletedAtUtcOffsetMinutes);

        Assert.Equal(
            -180,
            result.Items[1].CompletedAtUtcOffsetMinutes);

        _gymTimeZoneProvider
            .Received(1)
            .GetTimeZone(gymId);

        await _workoutExecutionRepository
            .Received(1)
            .GetHistoryByStudentAsync(
                student.Id,
                gymId,
                20,
                20);

        await _workoutExecutionRepository
            .Received(1)
            .CountHistoryByStudentAsync(
                student.Id,
                gymId);
    }

    [Fact]
    public async Task GetHistoryAsync_WithPageLessThanOne_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetHistoryAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                page: 0,
                pageSize: 20));

        await _studentRepository
            .DidNotReceive()
            .GetByIdAndGymIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetHistoryAsync_WithInvalidPageSize_ShouldThrowArgumentException(
    int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetHistoryAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                page: 1,
                pageSize: pageSize));

        await _workoutExecutionRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }


    [Fact]
    public async Task GetHistoryAsync_WhenStudentDoesNotExistInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetHistoryAsync(
                gymId,
                studentId,
                page: 1,
                pageSize: 20));

        await _workoutExecutionRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task GetActiveByStudentAsync_WhenStudentDoesNotExistInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetActiveByStudentAsync(
                gymId,
                studentId));

        await _workoutRepository
            .DidNotReceive()
            .GetActiveByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetActiveByStudentAsync_WhenThereIsNoActiveWorkout_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutRepository
            .GetActiveByStudentAsync(
                student.Id,
                gymId)
            .Returns((Workout?)null);

        var result =
            await _service.GetActiveByStudentAsync(
                gymId,
                student.Id);

        Assert.Null(result);

        await _workoutExecutionRepository
            .DidNotReceive()
            .GetLatestByWorkoutDayIdsAsync(
                Arg.Any<IEnumerable<Guid>>());
    }

    [Fact]
    public async Task GetActiveByStudentAsync_WhenWorkoutExists_ShouldReturnOrderedDetailAndCompletionState()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId, true);

        var exercise1 =
            CreateExercise(
                gymId,
                "Supino",
                "Peitoral",
                true);

        var exercise2 =
            CreateExercise(
                gymId,
                "Crucifixo",
                "Peitoral",
                true);

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            GymId = gymId,
            Name = "Treino Atual",
            Description = "Descrição",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var day2 = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia B",
            Order = 2
        };

        var day1 = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia A",
            Order = 1
        };

        day1.Exercises.Add(
            new WorkoutExercise
            {
                Id = Guid.NewGuid(),
                WorkoutDayId = day1.Id,
                WorkoutDay = day1,
                ExerciseId = exercise2.Id,
                Exercise = exercise2,
                Sets = 3,
                Repetitions = "12",
                RestSeconds = 60,
                Order = 2
            });

        day1.Exercises.Add(
            new WorkoutExercise
            {
                Id = Guid.NewGuid(),
                WorkoutDayId = day1.Id,
                WorkoutDay = day1,
                ExerciseId = exercise1.Id,
                Exercise = exercise1,
                Sets = 4,
                Repetitions = "8-10",
                RestSeconds = 90,
                Notes = "Controlado",
                Order = 1
            });

        workout.Days.Add(day2);
        workout.Days.Add(day1);

        var today =
            DateOnly.FromDateTime(
                DateTime.UtcNow);

        var completedAt =
            DateTime.UtcNow.AddMinutes(-30);

        var latestExecution =
            new WorkoutExecution
            {
                Id = Guid.NewGuid(),
                WorkoutDayId = day1.Id,
                WorkoutDay = day1,
                ExecutionDate = today,
                CompletedAt = completedAt
            };

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutRepository
            .GetActiveByStudentAsync(
                student.Id,
                gymId)
            .Returns(workout);

        _workoutExecutionRepository
            .GetLatestByWorkoutDayIdsAsync(
                Arg.Any<IEnumerable<Guid>>())
            .Returns(
                new List<WorkoutExecution>
                {
                latestExecution
                });

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var result =
            await _service.GetActiveByStudentAsync(
                gymId,
                student.Id);

        Assert.NotNull(result);

        Assert.Equal(workout.Id, result.Id);
        Assert.Equal("Treino Atual", result.Name);
        Assert.Equal(2, result.Days.Count);

        Assert.Equal("Dia A", result.Days[0].Name);
        Assert.Equal("Dia B", result.Days[1].Name);

        Assert.True(
            result.Days[0].CompletedToday);

        Assert.Equal(
            completedAt,
            result.Days[0].LastCompletedAt);

        Assert.Equal(
            0,
            result.Days[0].LastCompletedAtUtcOffsetMinutes);

        Assert.Null(
            result.Days[1].LastCompletedAt);

        Assert.Null(
            result.Days[1].LastCompletedAtUtcOffsetMinutes);

        _gymTimeZoneProvider
            .Received(1)
            .GetTimeZone(gymId);

        Assert.False(
            result.Days[1].CompletedToday);

        Assert.Null(
            result.Days[1].LastCompletedAt);

        Assert.Equal(
            exercise1.Id,
            result.Days[0].Exercises[0].ExerciseId);

        Assert.Equal(
            exercise2.Id,
            result.Days[0].Exercises[1].ExerciseId);

        await _workoutExecutionRepository
            .Received(1)
            .GetLatestByWorkoutDayIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(day1.Id) &&
                    ids.Contains(day2.Id) &&
                    ids.Count() == 2));
    }

    [Fact]
    public async Task GetActiveForUserAsync_WhenStudentDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetActiveForUserAsync(
                gymId,
                userId));

        await _workoutRepository
            .DidNotReceive()
            .GetActiveByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetActiveForUserAsync_ShouldResolveStudentUsingUserIdAndGymId()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            true);

        student.UserId = userId;
        student.User.Id = userId;

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutRepository
            .GetActiveByStudentAsync(
                student.Id,
                gymId)
            .Returns((Workout?)null);

        var result =
            await _service.GetActiveForUserAsync(
                gymId,
                userId);

        Assert.Null(result);

        await _studentRepository
            .Received(1)
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        await _workoutRepository
            .Received(1)
            .GetActiveByStudentAsync(
                student.Id,
                gymId);
    }

    [Fact]
    public async Task CompleteDayForUserAsync_WhenStudentDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CompleteDayForUserAsync(
                gymId,
                userId,
                Guid.NewGuid()));

        await _workoutExecutionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<WorkoutExecution>());
    }

    [Fact]
    public async Task CompleteDayForUserAsync_ShouldCompleteDayForResolvedStudent()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workoutDayId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            true);

        student.UserId = userId;
        student.User.Id = userId;

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId)
            .Returns(true);

        _workoutExecutionRepository
            .ExistsForWorkoutDayOnDateAsync(
                workoutDayId,
                Arg.Any<DateOnly>())
            .Returns(false);

        var result =
            await _service.CompleteDayForUserAsync(
                gymId,
                userId,
                workoutDayId);

        Assert.NotEqual(
            Guid.Empty,
            result.Id);

        Assert.Equal(
            workoutDayId,
            result.WorkoutDayId);

        await _studentRepository
            .Received(1)
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        await _workoutExecutionRepository
            .Received(1)
            .IsActiveWorkoutDayForStudentAsync(
                workoutDayId,
                student.Id,
                gymId);

        await _workoutExecutionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<WorkoutExecution>(
                    execution =>
                        execution.WorkoutDayId ==
                        workoutDayId));
    }

    [Fact]
    public async Task GetHistoryForUserAsync_WhenStudentDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetHistoryForUserAsync(
                gymId,
                userId,
                page: 1,
                pageSize: 20));

        await _workoutExecutionRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task GetHistoryForUserAsync_ShouldReturnHistoryForResolvedStudent()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            true);

        student.UserId = userId;
        student.User.Id = userId;

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _workoutExecutionRepository
            .GetHistoryByStudentAsync(
                student.Id,
                gymId,
                0,
                20)
            .Returns(
                new List<WorkoutExecution>());

        _workoutExecutionRepository
            .CountHistoryByStudentAsync(
                student.Id,
                gymId)
            .Returns(0);

        var result =
            await _service.GetHistoryForUserAsync(
                gymId,
                userId,
                page: 1,
                pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);

        await _studentRepository
            .Received(1)
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        await _workoutExecutionRepository
            .Received(1)
            .GetHistoryByStudentAsync(
                student.Id,
                gymId,
                0,
                20);
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkoutHasExecutions_ShouldCreateNewVersionAndPreserveHistory()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        var oldDay = workout.Days.Single();

        oldDay.Executions.Add(
            new WorkoutExecution
            {
                Id = Guid.NewGuid(),
                WorkoutDayId = oldDay.Id,
                WorkoutDay = oldDay,
                ExecutionDate = new DateOnly(2026, 8, 26),
                CompletedAt = DateTime.UtcNow.AddDays(-1)
            });

        var originalWorkoutId = workout.Id;
        var sourceTemplateId = Guid.NewGuid();

        workout.SourceWorkoutTemplateId = sourceTemplateId;

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _workoutExecutionRepository
            .ExistsForWorkoutAsync(
                 workout.Id,
                 gymId)
            .Returns(true);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var request = CreateValidUpdateWorkoutRequest(
            exercise.Id);

        request.Name = "  Treino Atualizado  ";
        request.Description = "  Nova descrição  ";

        Workout? capturedNewWorkout = null;

        _workoutRepository
            .When(x => x.AddAsync(
                Arg.Any<Workout>()))
            .Do(call =>
                capturedNewWorkout =
                    call.Arg<Workout>());

        var result = await _service.UpdateAsync(
            gymId,
            workout.Id,
            request);

        Assert.NotNull(capturedNewWorkout);

        Assert.False(workout.IsActive);
        Assert.NotNull(workout.UpdatedAt);

        Assert.NotEqual(
            originalWorkoutId,
            capturedNewWorkout.Id);

        Assert.Equal(
            workout.StudentId,
            capturedNewWorkout.StudentId);

        Assert.Equal(
            gymId,
            capturedNewWorkout.GymId);

        Assert.Equal(
            sourceTemplateId,
            capturedNewWorkout.SourceWorkoutTemplateId);

        Assert.Equal(
            "Treino Atualizado",
            capturedNewWorkout.Name);

        Assert.Equal(
            "Nova descrição",
            capturedNewWorkout.Description);

        Assert.True(capturedNewWorkout.IsActive);

        Assert.Single(capturedNewWorkout.Days);

        Assert.Equal(
            capturedNewWorkout.Id,
            result.Id);

        Assert.NotEqual(
            originalWorkoutId,
            result.Id);

        await _workoutRepository
            .Received(1)
            .AddAsync(capturedNewWorkout);

        await _workoutRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkoutHasNoExecutions_ShouldUpdateSameVersion()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        var originalWorkoutId = workout.Id;
        var existingDay = workout.Days.Single();
        var existingExercise =
            existingDay.Exercises.Single();

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var request = new UpdateWorkoutRequest
        {
            Name = "  Treino Atualizado  ",
            Description = "  Descrição nova  ",
            Days =
            [
                new UpdateWorkoutDayRequest
            {
                Id = existingDay.Id,
                Name = "  Dia Atualizado  ",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                    {
                        Id = existingExercise.Id,
                        ExerciseId = exercise.Id,
                        Sets = 4,
                        Repetitions = "  12  ",
                        RestSeconds = 90,
                        Notes = "  Controlado  ",
                        Order = 1
                    }
                ]
            }
            ]
        };

        var result = await _service.UpdateAsync(
            gymId,
            workout.Id,
            request);

        Assert.Equal(
            originalWorkoutId,
            result.Id);

        Assert.Equal(
            originalWorkoutId,
            workout.Id);

        Assert.True(workout.IsActive);

        Assert.Equal(
            "Treino Atualizado",
            workout.Name);

        Assert.Equal(
            "Descrição nova",
            workout.Description);

        Assert.NotNull(workout.UpdatedAt);

        var updatedDay =
            Assert.Single(workout.Days);

        Assert.Equal(
            existingDay.Id,
            updatedDay.Id);

        Assert.Equal(
            "Dia Atualizado",
            updatedDay.Name);

        var updatedExercise =
            Assert.Single(updatedDay.Exercises);

        Assert.Equal(
            existingExercise.Id,
            updatedExercise.Id);

        Assert.Equal(4, updatedExercise.Sets);
        Assert.Equal(
            "12",
            updatedExercise.Repetitions);

        Assert.Equal(
            90,
            updatedExercise.RestSeconds);

        Assert.Equal(
            "Controlado",
            updatedExercise.Notes);

        await _workoutRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Workout>());

        await _workoutRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkoutIsHistorical_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        workout.IsActive = false;

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                CreateValidUpdateWorkoutRequest(
                    exercise.Id)));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkoutIsNotFoundInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var workoutId = Guid.NewGuid();

        _workoutRepository
            .GetForUpdateAsync(
                workoutId,
                gymId)
            .Returns((Workout?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateAsync(
                gymId,
                workoutId,
                CreateValidUpdateWorkoutRequest(
                    Guid.NewGuid())));

        await _workoutRepository
            .Received(1)
            .GetForUpdateAsync(
                workoutId,
                gymId);

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithDayIdFromAnotherWorkout_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var request =
            CreateValidUpdateWorkoutRequest(
                exercise.Id);

        request.Days[0].Id =
            Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithExerciseIdThatDoesNotBelongToDay_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        var day = workout.Days.Single();

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var request =
            CreateValidUpdateWorkoutRequest(
                exercise.Id);

        request.Days[0].Id = day.Id;

        request.Days[0]
            .Exercises[0]
            .Id = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithInvalidName_ShouldThrowArgumentException(
    string name)
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Name = name;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithoutDays_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days.Clear();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithDayWithoutName_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0].Name = "   ";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateDayOrder_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days.Add(
            new UpdateWorkoutDayRequest
            {
                Name = "Outro Dia",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                {
                    ExerciseId = exercise.Id,
                    Sets = 3,
                    Repetitions = "10",
                    RestSeconds = 60,
                    Order = 1
                }
                ]
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateExistingDayId_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        var existingDayId =
            workout.Days.Single().Id;

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0].Id = existingDayId;

        request.Days.Add(
            new UpdateWorkoutDayRequest
            {
                Id = existingDayId,
                Name = "Outro Dia",
                Order = 2,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                {
                    ExerciseId = exercise.Id,
                    Sets = 3,
                    Repetitions = "10",
                    Order = 1
                }
                ]
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WithDayWithoutExercises_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0].Exercises.Clear();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateAsync_WithInvalidSets_ShouldThrowArgumentException(
    int sets)
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0].Exercises[0].Sets = sets;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithInvalidRepetitions_ShouldThrowArgumentException(
    string repetitions)
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0]
            .Exercises[0]
            .Repetitions = repetitions;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WithNegativeRestSeconds_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0]
            .Exercises[0]
            .RestSeconds = -1;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateExerciseOrder_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0].Exercises.Add(
            new UpdateWorkoutExerciseRequest
            {
                ExerciseId = exercise.Id,
                Sets = 3,
                Repetitions = "12",
                RestSeconds = 60,
                Order = 1
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateExistingWorkoutExerciseId_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        var existingExerciseId =
            workout.Days
                .Single()
                .Exercises
                .Single()
                .Id;

        _workoutRepository
            .GetForUpdateAsync(workout.Id, gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(exercise.Id);

        request.Days[0].Exercises[0].Id =
            existingExerciseId;

        request.Days[0].Exercises.Add(
            new UpdateWorkoutExerciseRequest
            {
                Id = existingExerciseId,
                ExerciseId = exercise.Id,
                Sets = 4,
                Repetitions = "12",
                RestSeconds = 60,
                Order = 2
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));
    }

    [Fact]
    public async Task UpdateAsync_WhenExerciseIsNotFoundInGym_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var existingExercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            existingExercise);

        var requestedExerciseId = Guid.NewGuid();

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(new List<Exercise>());

        var request =
            CreateValidUpdateWorkoutRequest(
                requestedExerciseId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        await _exerciseRepository
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(requestedExerciseId)),
                gymId);

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenExerciseIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            false);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var request =
            CreateValidUpdateWorkoutRequest(
                exercise.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        await _workoutRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkoutHasNoExecutions_ShouldRemoveOmittedDayAndAddNewDay()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise);

        var keptDay = workout.Days.Single();

        var removedDay = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia Removido",
            Order = 2
        };

        workout.Days.Add(removedDay);

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var existingWorkoutExercise =
            keptDay.Exercises.Single();

        var request = new UpdateWorkoutRequest
        {
            Name = "Treino Atualizado",
            Description = "Descrição atualizada",
            Days =
            [
                new UpdateWorkoutDayRequest
            {
                Id = keptDay.Id,
                Name = "Dia Mantido",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                    {
                        Id = existingWorkoutExercise.Id,
                        ExerciseId = exercise.Id,
                        Sets = 3,
                        Repetitions = "10",
                        RestSeconds = 60,
                        Order = 1
                    }
                ]
            },
            new UpdateWorkoutDayRequest
            {
                Name = "Dia Novo",
                Order = 2,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                    {
                        ExerciseId = exercise.Id,
                        Sets = 4,
                        Repetitions = "12",
                        RestSeconds = 90,
                        Order = 1
                    }
                ]
            }
            ]
        };

        var result = await _service.UpdateAsync(
            gymId,
            workout.Id,
            request);

        Assert.Equal(
            workout.Id,
            result.Id);

        Assert.Equal(
            2,
            workout.Days.Count);

        Assert.Contains(
            workout.Days,
            day => day.Id == keptDay.Id);

        Assert.DoesNotContain(
            workout.Days,
            day => day.Id == removedDay.Id);

        var newDay = Assert.Single(
            workout.Days,
            day => day.Id != keptDay.Id);

        Assert.Equal(
            "Dia Novo",
            newDay.Name);

        Assert.Single(
            newDay.Exercises);

        await _workoutRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkoutHasNoExecutions_ShouldRemoveOmittedExerciseAndAddNewExercise()
    {
        var gymId = Guid.NewGuid();

        var exercise1 = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        var exercise2 = CreateExercise(
            gymId,
            "Crucifixo",
            "Peitoral",
            true);

        var workout = CreateExistingWorkout(
            gymId,
            exercise1);

        var day = workout.Days.Single();

        var oldWorkoutExercise =
            day.Exercises.Single();

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise2
                });

        var request = new UpdateWorkoutRequest
        {
            Name = "Treino Atualizado",
            Days =
            [
                new UpdateWorkoutDayRequest
            {
                Id = day.Id,
                Name = "Dia Atualizado",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                    {
                        ExerciseId = exercise2.Id,
                        Sets = 4,
                        Repetitions = "12",
                        RestSeconds = 90,
                        Order = 1
                    }
                ]
            }
            ]
        };

        var result = await _service.UpdateAsync(
            gymId,
            workout.Id,
            request);

        Assert.Equal(
            workout.Id,
            result.Id);

        var updatedDay =
            Assert.Single(workout.Days);

        var updatedExercise =
            Assert.Single(updatedDay.Exercises);

        Assert.NotEqual(
            oldWorkoutExercise.Id,
            updatedExercise.Id);

        Assert.Equal(
            exercise2.Id,
            updatedExercise.ExerciseId);

        Assert.Equal(
            4,
            updatedExercise.Sets);

        Assert.Equal(
            "12",
            updatedExercise.Repetitions);

        Assert.DoesNotContain(
            updatedDay.Exercises,
            item =>
                item.Id ==
                oldWorkoutExercise.Id);

        await _workoutRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Theory]
    [InlineData("Name", "no máximo 150")]
    [InlineData("Description", "no máximo 500")]
    [InlineData("DayName", "no máximo 100")]
    [InlineData("Repetitions", "no máximo 50")]
    [InlineData("Notes", "no máximo 500")]
    public async Task CreateManualAsync_WhenPersistedTextExceedsDatabaseLimit_ShouldThrowCorrectArgumentException(
    string field,
    string expectedMessage)
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            true);

        var request = CreateValidManualRequest(
            student.Id,
            Guid.NewGuid());

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        switch (field)
        {
            case "Name":
                request.Name = new string('N', 151);
                break;

            case "Description":
                request.Description = new string('D', 501);
                break;

            case "DayName":
                request.Days[0].Name =
                    new string('D', 101);
                break;

            case "Repetitions":
                request.Days[0]
                    .Exercises[0]
                    .Repetitions =
                        new string('R', 51);
                break;

            case "Notes":
                request.Days[0]
                    .Exercises[0]
                    .Notes =
                        new string('N', 501);
                break;
        }

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateManualAsync(
                gymId,
                request));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameExceedsDatabaseLimit_ShouldThrowCorrectArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Supino",
            MuscleGroup = "Peitoral",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var workout =
            CreateExistingWorkout(gymId, exercise);

        _workoutRepository
            .GetForUpdateAsync(
                workout.Id,
                gymId)
            .Returns(workout);

        var request =
            CreateValidUpdateWorkoutRequest(
                exercise.Id);

        request.Name = new string('N', 151);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                workout.Id,
                request));

        Assert.Contains("no máximo 150", exception.Message);
    }

    private static UpdateWorkoutRequest CreateValidUpdateWorkoutRequest(
    Guid exerciseId)
    {
        return new UpdateWorkoutRequest
        {
            Name = "Treino Atualizado",
            Description = "Descrição atualizada",
            Days =
            [
                new UpdateWorkoutDayRequest
            {
                Name = "Dia Atualizado",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutExerciseRequest
                    {
                        ExerciseId = exerciseId,
                        Sets = 3,
                        Repetitions = "10",
                        RestSeconds = 60,
                        Order = 1
                    }
                ]
            }
            ]
        };
    }

    private static Workout CreateExistingWorkout(
        Guid gymId,
        Exercise exercise)
    {
        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            GymId = gymId,
            Name = "Treino Original",
            Description = "Descrição original",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var day = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia Original",
            Order = 1
        };

        var workoutExercise = new WorkoutExercise
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = day.Id,
            WorkoutDay = day,
            ExerciseId = exercise.Id,
            Exercise = exercise,
            Sets = 3,
            Repetitions = "10",
            RestSeconds = 60,
            Order = 1
        };

        day.Exercises.Add(workoutExercise);
        workout.Days.Add(day);

        return workout;
    }

    private static CreateWorkoutFromTemplateRequest CreateValidTemplateRequest(
    Guid studentId,
    Guid templateId,
    Guid exerciseId)
    {
        return new CreateWorkoutFromTemplateRequest
        {
            StudentId = studentId,
            TemplateId = templateId,
            Name = "Treino A",
            Description = "Descrição",
            Days =
            [
                new CreateWorkoutDayRequest
            {
                Name = "Dia A",
                Order = 1,
                Exercises =
                [
                    new CreateWorkoutExerciseRequest
                    {
                        ExerciseId = exerciseId,
                        Sets = 3,
                        Repetitions = "10",
                        RestSeconds = 60,
                        Order = 1
                    }
                ]
            }
            ]
        };
    }

    private static WorkoutTemplate CreateWorkoutTemplate(
        Guid gymId,
        string name,
        bool isActive)
    {
        return new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static CreateWorkoutRequest CreateValidManualRequest(
        Guid studentId,
        Guid exerciseId)
    {
        return new CreateWorkoutRequest
        {
            StudentId = studentId,
            Name = "Treino A",
            Description = "Descrição",
            Days =
            [
                new CreateWorkoutDayRequest
                {
                    Name = "Dia A",
                    Order = 1,
                    Exercises =
                    [
                        new CreateWorkoutExerciseRequest
                        {
                            ExerciseId = exerciseId,
                            Sets = 3,
                            Repetitions = "10",
                            RestSeconds = 60,
                            Order = 1
                        }
                    ]
                }
            ]
        };
    }

    private static Student CreateStudent(
        Guid gymId,
        bool isActive)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Aluno Teste",
            Email = "aluno@gymflow.dev",
            PasswordHash = "stored-hash",
            Role = UserRole.Student,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        return new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Exercise CreateExercise(
        Guid gymId,
        string name,
        string muscleGroup,
        bool isActive)
    {
        return new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            MuscleGroup = muscleGroup,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }
}