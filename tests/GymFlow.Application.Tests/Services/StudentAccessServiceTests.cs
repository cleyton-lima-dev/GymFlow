using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class StudentAccessServiceTests
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly StudentAccessService _service;
    private readonly IGymErpPolicy _erpPolicy;

    public StudentAccessServiceTests()
    {
        _studentRepository =
            Substitute.For<IStudentRepository>();

        _enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _erpPolicy = Substitute.For<IGymErpPolicy>();
        _service = new StudentAccessService(
            _studentRepository,
            _enrollmentRepository,
            _gymTimeZoneProvider,
            _erpPolicy);
    }

    [Fact]
    public async Task CanAccessApplicationAsync_LegacyTenant_DoesNotQueryEnrollmentOrTimeZone()
    {
        Assert.True(await _service.CanAccessApplicationAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Empty(_studentRepository.ReceivedCalls());
        Assert.Empty(_enrollmentRepository.ReceivedCalls());
        Assert.Empty(_gymTimeZoneProvider.ReceivedCalls());
    }

    [Fact]
    public async Task CanAccessApplicationAsync_EnabledTenant_DoesNotUseAnotherTenantsPolicy()
    {
        var enabledGym = Guid.NewGuid();
        var legacyGym = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _erpPolicy.IsEnabled(enabledGym).Returns(true);

        Assert.False(await _service.CanAccessApplicationAsync(userId, enabledGym));
        Assert.True(await _service.CanAccessApplicationAsync(userId, legacyGym));
        await _studentRepository.Received(1).GetByUserIdAndGymIdAsync(userId, enabledGym);
        await _studentRepository.DidNotReceive().GetByUserIdAndGymIdAsync(userId, legacyGym);
    }

    [Fact]
    public async Task HasActiveEnrollmentAsync_WhenStudentDoesNotExist_ShouldReturnFalse()
    {
        var userId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId)
            .Returns((Student?)null);

        var result =
            await _service.HasActiveEnrollmentAsync(
                userId,
                gymId);

        Assert.False(result);
    }

    [Fact]
    public async Task HasActiveEnrollmentAsync_WhenEnrollmentIsActive_ShouldReturnTrue()
    {
        var userId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        var student = new Student
        {
            Id = Guid.NewGuid()
        };

        _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId)
            .Returns(student);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .GetActiveByStudentAsync(
                student.Id,
                gymId,
                Arg.Any<DateOnly>())
            .Returns(new Enrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id
            });

        var result =
            await _service.HasActiveEnrollmentAsync(
                userId,
                gymId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasActiveEnrollmentAsync_WhenNoValidEnrollmentExists_ShouldReturnFalse()
    {
        var userId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        var student = new Student
        {
            Id = Guid.NewGuid()
        };

        _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId)
            .Returns(student);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .GetActiveByStudentAsync(
                student.Id,
                gymId,
                Arg.Any<DateOnly>())
            .Returns((Enrollment?)null);

        var result =
            await _service.HasActiveEnrollmentAsync(
                userId,
                gymId);

        Assert.False(result);
    }
}
