using GymFlow.Application.DTOs.WorkoutTemplates;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class WorkoutTemplateServiceTests
{
    private readonly IWorkoutTemplateRepository _workoutTemplateRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly WorkoutTemplateService _service;

    public WorkoutTemplateServiceTests()
    {
        _workoutTemplateRepository =
            Substitute.For<IWorkoutTemplateRepository>();

        _exerciseRepository =
            Substitute.For<IExerciseRepository>();

        _service = new WorkoutTemplateService(
            _workoutTemplateRepository,
            _exerciseRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateTemplate()
    {
        var gymId = Guid.NewGuid();

        var exercise1 = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral");

        var exercise2 = CreateExercise(
            gymId,
            "Tríceps Corda",
            "Tríceps");

        var request = new CreateWorkoutTemplateRequest
        {
            Name = "  Treino A  ",
            Description = "  Peito e tríceps  ",
            Days =
            [
                new CreateWorkoutTemplateDayRequest
                {
                    Name = "  Dia A  ",
                    Order = 1,
                    Exercises =
                    [
                        new CreateWorkoutTemplateExerciseRequest
                        {
                            ExerciseId = exercise2.Id,
                            Sets = 3,
                            Repetitions = "  12  ",
                            RestSeconds = 60,
                            Notes = "  Controlado  ",
                            Order = 2
                        },
                        new CreateWorkoutTemplateExerciseRequest
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

        _workoutTemplateRepository
            .ExistsByNameAsync(gymId, "Treino A")
            .Returns(false);

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

        var result = await _service.CreateAsync(
            gymId,
            request);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Treino A", result.Name);
        Assert.Equal(
            "Peito e tríceps",
            result.Description);
        Assert.True(result.IsActive);

        await _workoutTemplateRepository
            .Received(1)
            .AddAsync(
                Arg.Is<WorkoutTemplate>(template =>
                    template.GymId == gymId &&
                    template.Name == "Treino A" &&
                    template.Description == "Peito e tríceps" &&
                    template.IsActive &&
                    template.Days.Count == 1 &&
                    template.Days.Single().Name == "Dia A" &&
                    template.Days.Single().Exercises.Count == 2));

        await _exerciseRepository
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(exercise1.Id) &&
                    ids.Contains(exercise2.Id) &&
                    ids.Distinct().Count() == 2),
                gymId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidName_ShouldThrowArgumentException(
        string name)
    {
        var request = CreateValidRequest(
            Guid.NewGuid());

        request.Name = name;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WithoutDays_ShouldThrowArgumentException()
    {
        var request = new CreateWorkoutTemplateRequest
        {
            Name = "Treino A",
            Days = []
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExists_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();
        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral");

        var request =
            CreateValidRequest(exercise.Id);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                request.Name)
            .Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WhenExerciseIsNotFoundInGym_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var request =
            CreateValidRequest(exerciseId);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                request.Name)
            .Returns(false);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(new List<Exercise>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                gymId,
                request));

        await _exerciseRepository
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(exerciseId)),
                gymId);

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WhenExerciseIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral");

        exercise.IsActive = false;

        var request =
            CreateValidRequest(exercise.Id);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                request.Name)
            .Returns(false);

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
            () => _service.CreateAsync(
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WithDayWithoutName_ShouldThrowArgumentException()
    {
        var request = CreateValidRequest(
            Guid.NewGuid());

        request.Days[0].Name = "   ";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateDayOrder_ShouldThrowArgumentException()
    {
        var exerciseId = Guid.NewGuid();

        var request = CreateValidRequest(exerciseId);

        request.Days.Add(
            new CreateWorkoutTemplateDayRequest
            {
                Name = "Dia B",
                Order = 1,
                Exercises =
                [
                    new CreateWorkoutTemplateExerciseRequest
                {
                    ExerciseId = exerciseId,
                    Sets = 3,
                    Repetitions = "10",
                    RestSeconds = 60,
                    Order = 1
                }
                ]
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WithDayWithoutExercises_ShouldThrowArgumentException()
    {
        var request = CreateValidRequest(
            Guid.NewGuid());

        request.Days[0].Exercises.Clear();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_WithInvalidSets_ShouldThrowArgumentException(
    int sets)
    {
        var request = CreateValidRequest(
            Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .Sets = sets;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidRepetitions_ShouldThrowArgumentException(
    string repetitions)
    {
        var request = CreateValidRequest(
            Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .Repetitions = repetitions;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WithNegativeRestSeconds_ShouldThrowArgumentException()
    {
        var request = CreateValidRequest(
            Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .RestSeconds = -1;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateExerciseOrder_ShouldThrowArgumentException()
    {
        var exerciseId1 = Guid.NewGuid();
        var exerciseId2 = Guid.NewGuid();

        var request =
            CreateValidRequest(exerciseId1);

        request.Days[0]
            .Exercises.Add(
                new CreateWorkoutTemplateExerciseRequest
                {
                    ExerciseId = exerciseId2,
                    Sets = 3,
                    Repetitions = "12",
                    RestSeconds = 60,
                    Order = 1
                });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkoutTemplate>());
    }

    [Fact]
    public async Task GetAllAsync_WithValidParameters_ShouldReturnPagedTemplates()
    {
        var gymId = Guid.NewGuid();

        var template1 = new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Treino A",
            Description = "Descrição A",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var template2 = new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Treino B",
            Description = "Descrição B",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        _workoutTemplateRepository
            .GetPagedByGymAsync(
                gymId,
                "treino",
                true,
                20,
                20)
            .Returns(
                Task.FromResult(
                    (
                        new List<WorkoutTemplate>
                        {
                        template1,
                        template2
                        },
                        25
                    )));

        var result = await _service.GetAllAsync(
            gymId,
            search: "treino",
            isActive: true,
            page: 2,
            pageSize: 20);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(template1.Id, result.Items[0].Id);
        Assert.Equal("Treino A", result.Items[0].Name);
        Assert.True(result.Items[0].IsActive);

        Assert.Equal(template2.Id, result.Items[1].Id);
        Assert.False(result.Items[1].IsActive);

        await _workoutTemplateRepository
            .Received(1)
            .GetPagedByGymAsync(
                gymId,
                "treino",
                true,
                20,
                20);
    }

    [Fact]
    public async Task GetAllAsync_WithPageLessThanOne_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetAllAsync(
                Guid.NewGuid(),
                null,
                null,
                page: 0,
                pageSize: 20));

        await _workoutTemplateRepository
            .DidNotReceive()
            .GetPagedByGymAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetAllAsync_WithInvalidPageSize_ShouldThrowArgumentException(
    int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetAllAsync(
                Guid.NewGuid(),
                null,
                null,
                page: 1,
                pageSize: pageSize));

        await _workoutTemplateRepository
            .DidNotReceive()
            .GetPagedByGymAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenTemplateDoesNotExist_ShouldReturnNull()
    {
        var templateId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _workoutTemplateRepository
            .GetByIdAsync(templateId, gymId)
            .Returns((WorkoutTemplate?)null);

        var result = await _service.GetByIdAsync(
            templateId,
            gymId);

        Assert.Null(result);

        await _workoutTemplateRepository
            .Received(1)
            .GetByIdAsync(
                templateId,
                gymId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTemplateExists_ShouldReturnOrderedDaysAndExercises()
    {
        var gymId = Guid.NewGuid();

        var exercise1 = CreateExercise(
            gymId,
            "Supino",
            "Peitoral");

        var exercise2 = CreateExercise(
            gymId,
            "Crucifixo",
            "Peitoral");

        var template = new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Treino A",
            Description = "Descrição",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var day2 = new WorkoutTemplateDay
        {
            Id = Guid.NewGuid(),
            WorkoutTemplateId = template.Id,
            WorkoutTemplate = template,
            Name = "Dia B",
            Order = 2
        };

        var day1 = new WorkoutTemplateDay
        {
            Id = Guid.NewGuid(),
            WorkoutTemplateId = template.Id,
            WorkoutTemplate = template,
            Name = "Dia A",
            Order = 1
        };

        day1.Exercises.Add(
            new WorkoutTemplateExercise
            {
                Id = Guid.NewGuid(),
                WorkoutTemplateDayId = day1.Id,
                WorkoutTemplateDay = day1,
                ExerciseId = exercise2.Id,
                Exercise = exercise2,
                Sets = 3,
                Repetitions = "12",
                RestSeconds = 60,
                Order = 2
            });

        day1.Exercises.Add(
            new WorkoutTemplateExercise
            {
                Id = Guid.NewGuid(),
                WorkoutTemplateDayId = day1.Id,
                WorkoutTemplateDay = day1,
                ExerciseId = exercise1.Id,
                Exercise = exercise1,
                Sets = 4,
                Repetitions = "8-10",
                RestSeconds = 90,
                Notes = "Controlado",
                Order = 1
            });

        template.Days.Add(day2);
        template.Days.Add(day1);

        _workoutTemplateRepository
            .GetByIdAsync(
                template.Id,
                gymId)
            .Returns(template);

        var result = await _service.GetByIdAsync(
            template.Id,
            gymId);

        Assert.NotNull(result);

        Assert.Equal(template.Id, result.Id);
        Assert.Equal("Treino A", result.Name);
        Assert.Equal("Descrição", result.Description);

        Assert.Equal(2, result.Days.Count);

        Assert.Equal("Dia A", result.Days[0].Name);
        Assert.Equal(1, result.Days[0].Order);

        Assert.Equal("Dia B", result.Days[1].Name);
        Assert.Equal(2, result.Days[1].Order);

        Assert.Equal(
            2,
            result.Days[0].Exercises.Count);

        Assert.Equal(
            exercise1.Id,
            result.Days[0].Exercises[0].ExerciseId);

        Assert.Equal(
            "Supino",
            result.Days[0].Exercises[0].ExerciseName);

        Assert.Equal(
            "Peitoral",
            result.Days[0].Exercises[0].MuscleGroup);

        Assert.Equal(
            "Controlado",
            result.Days[0].Exercises[0].Notes);

        Assert.Equal(
            exercise2.Id,
            result.Days[0].Exercises[1].ExerciseId);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldReplaceDaysAndSaveChanges()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral");

        var template = CreateExistingTemplate(
            gymId,
            "Modelo Antigo");

        var oldDay = template.Days.Single();

        _workoutTemplateRepository
            .GetForUpdateAsync(template.Id, gymId)
            .Returns(template);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                "Modelo Atualizado",
                template.Id)
            .Returns(false);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(
                new List<Exercise>
                {
                exercise
                });

        var request = new UpdateWorkoutTemplateRequest
        {
            Name = "  Modelo Atualizado  ",
            Description = "  Nova descrição  ",
            Days =
            [
                new UpdateWorkoutTemplateDayRequest
            {
                Name = "  Novo Dia  ",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutTemplateExerciseRequest
                    {
                        ExerciseId = exercise.Id,
                        Sets = 4,
                        Repetitions = "  8-10  ",
                        RestSeconds = 90,
                        Notes = "  Controlado  ",
                        Order = 1
                    }
                ]
            }
            ]
        };

        var result = await _service.UpdateAsync(
            template.Id,
            gymId,
            request);

        Assert.True(result);

        Assert.Equal(
            "Modelo Atualizado",
            template.Name);

        Assert.Equal(
            "Nova descrição",
            template.Description);

        Assert.NotNull(template.UpdatedAt);

        var newDay = Assert.Single(template.Days);

        Assert.NotSame(oldDay, newDay);
        Assert.Equal("Novo Dia", newDay.Name);
        Assert.Equal(1, newDay.Order);

        var newExercise =
            Assert.Single(newDay.Exercises);

        Assert.Equal(
            exercise.Id,
            newExercise.ExerciseId);

        Assert.Equal(4, newExercise.Sets);
        Assert.Equal("8-10", newExercise.Repetitions);
        Assert.Equal(90, newExercise.RestSeconds);
        Assert.Equal("Controlado", newExercise.Notes);

        await _workoutTemplateRepository
            .Received(1)
            .RemoveDaysAsync(
                Arg.Is<IEnumerable<WorkoutTemplateDay>>(
                    days =>
                        days.Count() == 1 &&
                        days.Single() == oldDay));

        _workoutTemplateRepository
            .Received(1)
            .AddDays(
                Arg.Is<IEnumerable<WorkoutTemplateDay>>(
                    days =>
                        days.Count() == 1 &&
                        days.Single().Name == "Novo Dia"));

        await _workoutTemplateRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenTemplateDoesNotExist_ShouldReturnFalse()
    {
        var templateId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _workoutTemplateRepository
            .GetForUpdateAsync(
                templateId,
                gymId)
            .Returns((WorkoutTemplate?)null);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        var result = await _service.UpdateAsync(
            templateId,
            gymId,
            request);

        Assert.False(result);

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();

        _workoutTemplateRepository
            .DidNotReceive()
            .AddDays(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());
    }

    [Fact]
    public async Task UpdateAsync_WhenAnotherTemplateHasSameName_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        var exerciseId = Guid.NewGuid();

        var request =
            CreateValidUpdateRequest(exerciseId);

        request.Name = "Modelo Existente";

        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                "Modelo Existente",
                template.Id)
            .Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .RemoveDaysAsync(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }



    [Fact]
    public async Task UpdateAsync_WhenExerciseIsNotFoundInGym_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        var exerciseId = Guid.NewGuid();

        var request =
            CreateValidUpdateRequest(exerciseId);

        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                request.Name,
                template.Id)
            .Returns(false);

        _exerciseRepository
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                gymId)
            .Returns(new List<Exercise>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _exerciseRepository
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IEnumerable<Guid>>(
                    ids => ids.Contains(exerciseId)),
                gymId);

        await _workoutTemplateRepository
            .DidNotReceive()
            .RemoveDaysAsync(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenExerciseIsInactive_ShouldThrowInvalidOperationException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral");

        exercise.IsActive = false;

        var request =
            CreateValidUpdateRequest(
                exercise.Id);

        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                request.Name,
                template.Id)
            .Returns(false);

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
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .RemoveDaysAsync(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }



    [Fact]
    public async Task SetActiveStatusAsync_ShouldDelegateToRepositoryAndReturnTrue()
    {
        var templateId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _workoutTemplateRepository
            .SetActiveStatusAsync(
                templateId,
                gymId,
                false)
            .Returns(true);

        var result =
            await _service.SetActiveStatusAsync(
                templateId,
                gymId,
                false);

        Assert.True(result);

        await _workoutTemplateRepository
            .Received(1)
            .SetActiveStatusAsync(
                templateId,
                gymId,
                false);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenRepositoryReturnsFalse_ShouldReturnFalse()
    {
        var templateId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _workoutTemplateRepository
            .SetActiveStatusAsync(
                templateId,
                gymId,
                true)
            .Returns(false);

        var result =
            await _service.SetActiveStatusAsync(
                templateId,
                gymId,
                true);

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithInvalidName_ShouldThrowArgumentException(
    string name)
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Name = name;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithoutDays_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Days.Clear();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithDayWithoutName_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Days[0].Name = "   ";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _exerciseRepository
            .DidNotReceive()
            .GetByIdsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Guid>());

        await _workoutTemplateRepository
            .DidNotReceive()
            .RemoveDaysAsync(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());
    }



    [Fact]
    public async Task UpdateAsync_WithDuplicateDayOrder_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var exerciseId = Guid.NewGuid();

        var request =
            CreateValidUpdateRequest(exerciseId);

        request.Days.Add(
            new UpdateWorkoutTemplateDayRequest
            {
                Name = "Outro Dia",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutTemplateExerciseRequest
                {
                    ExerciseId = exerciseId,
                    Sets = 3,
                    Repetitions = "10",
                    RestSeconds = 60,
                    Order = 1
                }
                ]
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .RemoveDaysAsync(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());
    }

    [Fact]
    public async Task UpdateAsync_WithDayWithoutExercises_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Days[0].Exercises.Clear();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .RemoveDaysAsync(
                Arg.Any<IEnumerable<WorkoutTemplateDay>>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateAsync_WithInvalidSets_ShouldThrowArgumentException(
    int sets)
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .Sets = sets;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithInvalidRepetitions_ShouldThrowArgumentException(
    string repetitions)
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .Repetitions = repetitions;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }



    [Fact]
    public async Task UpdateAsync_WithNegativeRestSeconds_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var request =
            CreateValidUpdateRequest(
                Guid.NewGuid());

        request.Days[0]
            .Exercises[0]
            .RestSeconds = -1;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateExerciseOrder_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template =
            CreateExistingTemplate(
                gymId,
                "Modelo Antigo");

        ArrangeTemplateForUpdateValidation(
            template,
            gymId);

        var exerciseId1 = Guid.NewGuid();
        var exerciseId2 = Guid.NewGuid();

        var request =
            CreateValidUpdateRequest(
                exerciseId1);

        request.Days[0]
            .Exercises.Add(
                new UpdateWorkoutTemplateExerciseRequest
                {
                    ExerciseId = exerciseId2,
                    Sets = 3,
                    Repetitions = "12",
                    RestSeconds = 60,
                    Order = 1
                });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        await _workoutTemplateRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }



    private void ArrangeTemplateForUpdateValidation(
    WorkoutTemplate template,
    Guid gymId)
    {
        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        _workoutTemplateRepository
            .ExistsByNameAsync(
                gymId,
                "Modelo Atualizado",
                template.Id)
            .Returns(false);
    }

    [Theory]
    [InlineData("Name", "no máximo 150")]
    [InlineData("Description", "no máximo 500")]
    [InlineData("DayName", "no máximo 100")]
    [InlineData("Repetitions", "no máximo 50")]
    [InlineData("Notes", "no máximo 500")]
    public async Task CreateAsync_WhenPersistedTextExceedsDatabaseLimit_ShouldThrowCorrectArgumentException(
    string field,
    string expectedMessage)
    {
        var request = CreateValidRequest(Guid.NewGuid());

        switch (field)
        {
            case "Name":
                request.Name = new string('N', 151);
                break;

            case "Description":
                request.Description = new string('D', 501);
                break;

            case "DayName":
                request.Days[0].Name = new string('D', 101);
                break;

            case "Repetitions":
                request.Days[0]
                    .Exercises[0]
                    .Repetitions = new string('R', 51);
                break;

            case "Notes":
                request.Days[0]
                    .Exercises[0]
                    .Notes = new string('N', 501);
                break;
        }

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameExceedsDatabaseLimit_ShouldThrowCorrectArgumentException()
    {
        var gymId = Guid.NewGuid();

        var template = CreateExistingTemplate(
            gymId,
            "Modelo Existente");

        _workoutTemplateRepository
            .GetForUpdateAsync(
                template.Id,
                gymId)
            .Returns(template);

        var request =
            CreateValidUpdateRequest(Guid.NewGuid());

        request.Name = new string('N', 151);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                template.Id,
                gymId,
                request));

        Assert.Contains("no máximo 150", exception.Message);
    }

    private static UpdateWorkoutTemplateRequest CreateValidUpdateRequest(
    Guid exerciseId)
    {
        return new UpdateWorkoutTemplateRequest
        {
            Name = "Modelo Atualizado",
            Description = "Descrição atualizada",
            Days =
            [
                new UpdateWorkoutTemplateDayRequest
            {
                Name = "Dia Atualizado",
                Order = 1,
                Exercises =
                [
                    new UpdateWorkoutTemplateExerciseRequest
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

    private static WorkoutTemplate CreateExistingTemplate(
        Guid gymId,
        string name)
    {
        var template = new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            Description = "Descrição antiga",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var day = new WorkoutTemplateDay
        {
            Id = Guid.NewGuid(),
            WorkoutTemplateId = template.Id,
            WorkoutTemplate = template,
            Name = "Dia Antigo",
            Order = 1
        };

        template.Days.Add(day);

        return template;
    }

    private static CreateWorkoutTemplateRequest CreateValidRequest(
        Guid exerciseId)
    {
        return new CreateWorkoutTemplateRequest
        {
            Name = "Treino A",
            Description = "Descrição",
            Days =
            [
                new CreateWorkoutTemplateDayRequest
                {
                    Name = "Dia A",
                    Order = 1,
                    Exercises =
                    [
                        new CreateWorkoutTemplateExerciseRequest
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

    private static Exercise CreateExercise(
        Guid gymId,
        string name,
        string muscleGroup)
    {
        return new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            MuscleGroup = muscleGroup,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}