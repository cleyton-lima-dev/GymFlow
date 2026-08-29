using GymFlow.Application.DTOs.Exercises;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class ExerciseServiceTests
{
    private readonly IExerciseRepository _exerciseRepository;
    private readonly ExerciseService _service;

    public ExerciseServiceTests()
    {
        _exerciseRepository =
            Substitute.For<IExerciseRepository>();

        _service = new ExerciseService(
            _exerciseRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateExercise()
    {
        var gymId = Guid.NewGuid();

        var request = new CreateExerciseRequest
        {
            Name = "  Supino Reto  ",
            MuscleGroup = "  Peitoral  ",
            Description = "  Barra livre  "
        };

        _exerciseRepository
            .GetByNameAsync("Supino Reto", gymId)
            .Returns((Exercise?)null);

        var result = await _service.CreateAsync(
            gymId,
            request);

        Assert.True(result);

        await _exerciseRepository
            .Received(1)
            .AddAsync(
                Arg.Is<Exercise>(exercise =>
                    exercise.GymId == gymId &&
                    exercise.Name == "Supino Reto" &&
                    exercise.MuscleGroup == "Peitoral" &&
                    exercise.Description == "Barra livre" &&
                    exercise.IsActive &&
                    exercise.Id != Guid.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidName_ShouldReturnFalse(
        string name)
    {
        var request = new CreateExerciseRequest
        {
            Name = name,
            MuscleGroup = "Peitoral"
        };

        var result = await _service.CreateAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result);

        await _exerciseRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Exercise>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidMuscleGroup_ShouldReturnFalse(
        string muscleGroup)
    {
        var request = new CreateExerciseRequest
        {
            Name = "Supino Reto",
            MuscleGroup = muscleGroup
        };

        var result = await _service.CreateAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result);

        await _exerciseRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Exercise>());
    }

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExistsInGym_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();

        var existing = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral",
            true);

        _exerciseRepository
            .GetByNameAsync("Supino Reto", gymId)
            .Returns(existing);

        var request = new CreateExerciseRequest
        {
            Name = "  Supino Reto  ",
            MuscleGroup = "Peitoral"
        };

        var result = await _service.CreateAsync(
            gymId,
            request);

        Assert.False(result);

        await _exerciseRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Exercise>());
    }

    [Fact]
    public async Task ListAsync_WithValidParameters_ShouldReturnPagedExercises()
    {
        var gymId = Guid.NewGuid();

        var exercise1 = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral",
            true);

        var exercise2 = CreateExercise(
            gymId,
            "Crucifixo",
            "Peitoral",
            false);

        _exerciseRepository
            .GetPagedByGymAsync(
                gymId,
                "supino",
                "Peitoral",
                true,
                20,
                20)
            .Returns(
                Task.FromResult(
                    (
                        new List<Exercise>
                        {
                            exercise1,
                            exercise2
                        },
                        25
                    )));

        var result = await _service.ListAsync(
            gymId,
            search: "supino",
            muscleGroup: "Peitoral",
            isActive: true,
            page: 2,
            pageSize: 20);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(
            exercise1.Id,
            result.Items[0].Id);

        Assert.Equal(
            "Supino Reto",
            result.Items[0].Name);

        Assert.True(
            result.Items[0].IsActive);

        Assert.False(
            result.Items[1].IsActive);

        await _exerciseRepository
            .Received(1)
            .GetPagedByGymAsync(
                gymId,
                "supino",
                "Peitoral",
                true,
                20,
                20);
    }

    [Fact]
    public async Task ListAsync_WithPageLessThanOne_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListAsync(
                Guid.NewGuid(),
                page: 0,
                pageSize: 20));

        await _exerciseRepository
            .DidNotReceive()
            .GetPagedByGymAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListAsync_WithInvalidPageSize_ShouldThrowArgumentException(
        int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListAsync(
                Guid.NewGuid(),
                page: 1,
                pageSize: pageSize));

        await _exerciseRepository
            .DidNotReceive()
            .GetPagedByGymAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateExercise()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Nome Antigo",
            "Peitoral",
            true);

        _exerciseRepository
            .GetByIdAsync(exercise.Id, gymId)
            .Returns(exercise);

        _exerciseRepository
            .GetByNameAsync("Supino Inclinado", gymId)
            .Returns((Exercise?)null);

        var request = new UpdateExerciseRequest
        {
            Name = "  Supino Inclinado  ",
            MuscleGroup = "  Peitoral  ",
            Description = "  Halteres  "
        };

        var result = await _service.UpdateAsync(
            gymId,
            exercise.Id,
            request);

        Assert.True(result);

        Assert.Equal(
            "Supino Inclinado",
            exercise.Name);

        Assert.Equal(
            "Peitoral",
            exercise.MuscleGroup);

        Assert.Equal(
            "Halteres",
            exercise.Description);

        Assert.NotNull(exercise.UpdatedAt);

        await _exerciseRepository
            .Received(1)
            .UpdateAsync(exercise);
    }

    [Fact]
    public async Task UpdateAsync_WhenExerciseDoesNotExist_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _exerciseRepository
            .GetByIdAsync(exerciseId, gymId)
            .Returns((Exercise?)null);

        var request = new UpdateExerciseRequest
        {
            Name = "Supino",
            MuscleGroup = "Peitoral"
        };

        var result = await _service.UpdateAsync(
            gymId,
            exerciseId,
            request);

        Assert.False(result);

        await _exerciseRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Exercise>());
    }

    [Fact]
    public async Task UpdateAsync_WhenNameBelongsToAnotherExercise_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral",
            true);

        var anotherExercise = CreateExercise(
            gymId,
            "Supino Inclinado",
            "Peitoral",
            true);

        _exerciseRepository
            .GetByIdAsync(exercise.Id, gymId)
            .Returns(exercise);

        _exerciseRepository
            .GetByNameAsync(
                "Supino Inclinado",
                gymId)
            .Returns(anotherExercise);

        var request = new UpdateExerciseRequest
        {
            Name = "Supino Inclinado",
            MuscleGroup = "Peitoral"
        };

        var result = await _service.UpdateAsync(
            gymId,
            exercise.Id,
            request);

        Assert.False(result);

        await _exerciseRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Exercise>());
    }

    [Fact]
    public async Task UpdateAsync_WhenNameBelongsToSameExercise_ShouldAllowUpdate()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral",
            true);

        _exerciseRepository
            .GetByIdAsync(exercise.Id, gymId)
            .Returns(exercise);

        _exerciseRepository
            .GetByNameAsync(
                "Supino Reto",
                gymId)
            .Returns(exercise);

        var request = new UpdateExerciseRequest
        {
            Name = "Supino Reto",
            MuscleGroup = "Peitoral",
            Description = "Atualizado"
        };

        var result = await _service.UpdateAsync(
            gymId,
            exercise.Id,
            request);

        Assert.True(result);

        Assert.Equal(
            "Atualizado",
            exercise.Description);

        await _exerciseRepository
            .Received(1)
            .UpdateAsync(exercise);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenExerciseExists_ShouldUpdateStatus()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino Reto",
            "Peitoral",
            true);

        _exerciseRepository
            .GetByIdAsync(exercise.Id, gymId)
            .Returns(exercise);

        var result =
            await _service.UpdateStatusAsync(
                gymId,
                exercise.Id,
                false);

        Assert.True(result);
        Assert.False(exercise.IsActive);
        Assert.NotNull(exercise.UpdatedAt);

        await _exerciseRepository
            .Received(1)
            .UpdateAsync(exercise);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenExerciseDoesNotExist_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _exerciseRepository
            .GetByIdAsync(exerciseId, gymId)
            .Returns((Exercise?)null);

        var result =
            await _service.UpdateStatusAsync(
                gymId,
                exerciseId,
                false);

        Assert.False(result);

        await _exerciseRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Exercise>());
    }

    [Theory]
    [InlineData("Name", "no máximo 150")]
    [InlineData("MuscleGroup", "no máximo 100")]
    [InlineData("Description", "no máximo 500")]
    public async Task CreateAsync_WhenPersistedTextExceedsDatabaseLimit_ShouldThrowArgumentException(
    string field,
    string expectedMessage)
    {
        var request = new CreateExerciseRequest
        {
            Name = "Supino Reto",
            MuscleGroup = "Peitoral",
            Description = "Barra livre"
        };

        switch (field)
        {
            case "Name":
                request.Name = new string('N', 151);
                break;

            case "MuscleGroup":
                request.MuscleGroup = new string('M', 101);
                break;

            case "Description":
                request.Description = new string('D', 501);
                break;
        }

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameExceedsDatabaseLimit_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var exercise = CreateExercise(
            gymId,
            "Supino",
            "Peitoral",
            true);

        _exerciseRepository
            .GetByIdAsync(exercise.Id, gymId)
            .Returns(exercise);

        var request = new UpdateExerciseRequest
        {
            Name = new string('N', 151),
            MuscleGroup = "Peitoral",
            Description = "Descrição"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                gymId,
                exercise.Id,
                request));

        Assert.Contains("no máximo 150", exception.Message);
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
            Description = "Descrição",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }
}
