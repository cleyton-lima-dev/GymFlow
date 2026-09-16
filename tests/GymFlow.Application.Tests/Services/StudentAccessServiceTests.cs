using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
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

    public StudentAccessServiceTests()
    {
        _studentRepository =
            Substitute.For<IStudentRepository>();

        _enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _service = new StudentAccessService(
            _studentRepository,
            _enrollmentRepository,
            _gymTimeZoneProvider);
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