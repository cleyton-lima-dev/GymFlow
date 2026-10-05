using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class PhysicalAccessCredentialServiceTests
{
    private readonly IPhysicalAccessCredentialRepository
        _credentialRepository;

    private readonly IStudentRepository
        _studentRepository;

    private readonly PhysicalAccessCredentialService
        _service;

    public PhysicalAccessCredentialServiceTests()
    {
        _credentialRepository =
            Substitute.For<IPhysicalAccessCredentialRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _service =
            new PhysicalAccessCredentialService(
                _credentialRepository,
                _studentRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateCredential()
    {
        var gymId = Guid.NewGuid();
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

        var request =
            new CreatePhysicalAccessCredentialRequest
            {
                StudentId = studentId,
                Type =
                    PhysicalAccessCredentialType
                        .BiometricExternalId,
                ProviderKey = "  toletus  ",
                ExternalIdentifier = "  1847  "
            };

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns(student);

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType
                    .BiometricExternalId,
                "1847")
            .Returns(
                (PhysicalAccessCredential?)null);

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.NotNull(result);

        Assert.Equal(
            studentId,
            result.StudentId);

        Assert.Equal(
            "toletus",
            result.ProviderKey);

        Assert.Equal(
            "1847",
            result.ExternalIdentifier);

        Assert.True(result.IsActive);

        await _credentialRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PhysicalAccessCredential>(
                    credential =>
                        credential.GymId == gymId &&
                        credential.StudentId ==
                            studentId &&
                        credential.ProviderKey ==
                            "toletus" &&
                        credential.ExternalIdentifier ==
                            "1847" &&
                        credential.IsActive));
    }

    [Fact]
    public async Task CreateAsync_WhenCredentialAlreadyExists_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
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

        var request =
            new CreatePhysicalAccessCredentialRequest
            {
                StudentId = studentId,
                Type =
                    PhysicalAccessCredentialType.Card,
                ProviderKey = "toletus",
                ExternalIdentifier = "ABC123"
            };

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns(student);

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.Card,
                "ABC123")
            .Returns(
                new PhysicalAccessCredential
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    StudentId = Guid.NewGuid(),
                    Type =
                        PhysicalAccessCredentialType.Card,
                    ProviderKey = "toletus",
                    ExternalIdentifier = "ABC123"
                });

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.Null(result);

        await _credentialRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAccessCredential>());
    }

    [Fact]
    public async Task CreateAsync_WhenStudentDoesNotBelongToGym_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        var result =
            await _service.CreateAsync(
                gymId,
                new CreatePhysicalAccessCredentialRequest
                {
                    StudentId = studentId,
                    Type =
                        PhysicalAccessCredentialType
                            .BiometricExternalId,
                    ProviderKey = "toletus",
                    ExternalIdentifier = "1847"
                });

        Assert.Null(result);

        await _credentialRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAccessCredential>());
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenCredentialExists_ShouldUpdateStatus()
    {
        var gymId = Guid.NewGuid();

        var credential =
            new PhysicalAccessCredential
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = Guid.NewGuid(),
                Type =
                    PhysicalAccessCredentialType
                        .BiometricExternalId,
                ProviderKey = "toletus",
                ExternalIdentifier = "1847",
                IsActive = true
            };

        _credentialRepository
            .GetByIdAsync(
                credential.Id,
                gymId)
            .Returns(credential);

        var result =
            await _service.UpdateStatusAsync(
                gymId,
                credential.Id,
                false);

        Assert.True(result);
        Assert.False(credential.IsActive);
        Assert.NotNull(credential.UpdatedAt);

        await _credentialRepository
            .Received(1)
            .UpdateAsync(credential);
    }
}