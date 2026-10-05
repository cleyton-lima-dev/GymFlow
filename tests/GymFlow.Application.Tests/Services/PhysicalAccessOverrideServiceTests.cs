using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class PhysicalAccessOverrideServiceTests
{
    private readonly IPhysicalAccessOverrideRepository
        _overrideRepository;

    private readonly IStudentRepository
        _studentRepository;

    private readonly PhysicalAccessOverrideService
        _service;

    public PhysicalAccessOverrideServiceTests()
    {
        _overrideRepository =
            Substitute.For<IPhysicalAccessOverrideRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _service =
            new PhysicalAccessOverrideService(
                _overrideRepository,
                _studentRepository);
    }

    [Fact]
    public async Task SetAsync_WhenOverrideDoesNotExist_ShouldCreateOverride()
    {
        var gymId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns(student);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns((PhysicalAccessOverride?)null);

        var result =
            await _service.SetAsync(
                gymId,
                actorUserId,
                studentId,
                new SetPhysicalAccessOverrideRequest
                {
                    Type =
                        PhysicalAccessOverrideType.Allow,
                    Reason = "  Vai pagar após o treino  "
                });

        Assert.NotNull(result);
        Assert.Equal(
            PhysicalAccessOverrideType.Allow,
            result.Type);
        Assert.Equal(
            "Vai pagar após o treino",
            result.Reason);
        Assert.Equal(
            actorUserId,
            result.ActorUserId);

        await _overrideRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PhysicalAccessOverride>(
                    accessOverride =>
                        accessOverride.GymId == gymId &&
                        accessOverride.StudentId ==
                            studentId &&
                        accessOverride.ActorUserId ==
                            actorUserId &&
                        accessOverride.Type ==
                            PhysicalAccessOverrideType.Allow &&
                        accessOverride.Reason ==
                            "Vai pagar após o treino"));
    }

    [Fact]
    public async Task SetAsync_WhenOverrideAlreadyExists_ShouldUpdateOverride()
    {
        var gymId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var existingOverride =
            new PhysicalAccessOverride
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = studentId,
                Type =
                    PhysicalAccessOverrideType.Block,
                Reason = "Bloqueio anterior",
                ActorUserId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns(student);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns(existingOverride);

        var result =
            await _service.SetAsync(
                gymId,
                actorUserId,
                studentId,
                new SetPhysicalAccessOverrideRequest
                {
                    Type =
                        PhysicalAccessOverrideType.Allow,
                    Reason = "Liberado excepcionalmente"
                });

        Assert.NotNull(result);

        Assert.Equal(
            PhysicalAccessOverrideType.Allow,
            existingOverride.Type);

        Assert.Equal(
            "Liberado excepcionalmente",
            existingOverride.Reason);

        Assert.Equal(
            actorUserId,
            existingOverride.ActorUserId);

        Assert.NotNull(existingOverride.UpdatedAt);

        await _overrideRepository
            .Received(1)
            .UpdateAsync(existingOverride);
    }

    [Fact]
    public async Task SetAsync_WhenStudentDoesNotBelongToGym_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        var result =
            await _service.SetAsync(
                gymId,
                Guid.NewGuid(),
                studentId,
                new SetPhysicalAccessOverrideRequest
                {
                    Type =
                        PhysicalAccessOverrideType.Block
                });

        Assert.Null(result);

        await _overrideRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAccessOverride>());
    }

    [Fact]
    public async Task RemoveAsync_WhenOverrideExists_ShouldDeleteOverride()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var existingOverride =
            new PhysicalAccessOverride
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = studentId,
                Type =
                    PhysicalAccessOverrideType.Allow,
                ActorUserId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns(existingOverride);

        var result =
            await _service.RemoveAsync(
                gymId,
                studentId);

        Assert.True(result);

        await _overrideRepository
            .Received(1)
            .DeleteAsync(existingOverride);
    }
}