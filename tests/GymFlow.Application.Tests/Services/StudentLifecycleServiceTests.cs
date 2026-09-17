using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class StudentLifecycleServiceTests
{
    [Fact]
    public async Task ProcessAsync_WhenStudentHas30DaysWithoutValidEnrollment_ShouldInactivateAutomatically()
    {
        var gymId = Guid.NewGuid();

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            IsActive = true
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            NoValidEnrollmentSince = today.AddDays(-30)
        };

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment>());

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.False(student.User.IsActive);

        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);

        Assert.NotNull(student.InactivatedAt);

        await studentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenStudentHas29DaysWithoutValidEnrollment_ShouldRemainActive()
    {
        var gymId = Guid.NewGuid();

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            IsActive = true
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            NoValidEnrollmentSince = today.AddDays(-29)
        };

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment>());

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.True(student.User.IsActive);
        Assert.Null(student.InactivationReason);
        Assert.Null(student.InactivatedAt);

        await studentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenStudentHas90DaysWithoutValidEnrollment_ShouldArchive()
    {
        var gymId = Guid.NewGuid();

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            IsActive = false
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            NoValidEnrollmentSince = today.AddDays(-90),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-60)
        };

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment>());

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.False(student.User.IsActive);
        Assert.NotNull(student.ArchivedAt);

        await studentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenAutomaticallyInactiveStudentGetsValidEnrollment_ShouldReactivate()
    {
        var gymId = Guid.NewGuid();

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            IsActive = false
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            Status = EnrollmentStatus.Active,
            StartDate = today,
            EndDate = today.AddMonths(1)
        };

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment> { enrollment });

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.True(student.User.IsActive);
        Assert.Null(student.NoValidEnrollmentSince);
        Assert.Null(student.InactivationReason);
        Assert.Null(student.InactivatedAt);

        await studentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenManuallyInactiveStudentGetsValidEnrollment_ShouldRemainInactive()
    {
        var gymId = Guid.NewGuid();

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            IsActive = false
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason =
                StudentInactivationReason.Manual,
            InactivatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            Status = EnrollmentStatus.Active,
            StartDate = today,
            EndDate = today.AddMonths(1)
        };

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment> { enrollment });

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.False(student.User.IsActive);
        Assert.Null(student.NoValidEnrollmentSince);

        Assert.Equal(
            StudentInactivationReason.Manual,
            student.InactivationReason);

        Assert.NotNull(student.InactivatedAt);

        await studentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenEnrollmentExpiredDaysAgo_ShouldStartCounterFromExpirationDate()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expirationDate = today.AddDays(-5);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = null,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            StartDate = today.AddMonths(-1),
            EndDate = expirationDate,
            Status = EnrollmentStatus.Active
        };

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment> { enrollment });

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.Equal(
            expirationDate,
            student.NoValidEnrollmentSince);
    }

    [Fact]
    public async Task ProcessAsync_WhenEnrollmentExpiredMoreThan30DaysAgoAndCounterIsNull_ShouldInactivateImmediately()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expirationDate = today.AddDays(-35);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = null,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            StartDate = today.AddMonths(-2),
            EndDate = expirationDate,
            Status = EnrollmentStatus.Active
        };

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment> { enrollment });

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.False(student.User.IsActive);
        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);
        Assert.Equal(
            expirationDate,
            student.NoValidEnrollmentSince);
    }

    [Fact]
    public async Task ProcessAsync_WhenEnrollmentExpiredMoreThan90DaysAgoAndCounterIsNull_ShouldArchiveImmediately()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expirationDate = today.AddDays(-95);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = null,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            StartDate = today.AddMonths(-6),
            EndDate = expirationDate,
            Status = EnrollmentStatus.Active
        };

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment> { enrollment });

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.False(student.User.IsActive);
        Assert.NotNull(student.ArchivedAt);

        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);

        Assert.Equal(
            expirationDate,
            student.NoValidEnrollmentSince);
    }

    [Fact]
    public async Task ProcessAsync_WhenEnrollmentWasCancelledDaysAgo_ShouldStartCounterFromCancellationDate()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cancellationDate = today.AddDays(-7);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = null,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            StartDate = today.AddMonths(-1),
            EndDate = today.AddMonths(1),
            Status = EnrollmentStatus.Cancelled,
            CancellationDate = cancellationDate
        };

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment> { enrollment });

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.Equal(
            cancellationDate,
            student.NoValidEnrollmentSince);

        Assert.True(student.User.IsActive);
    }

    [Fact]
    public async Task ProcessAsync_WhenStudentIsAlreadyAutomaticallyInactive_ShouldNotChangeAgain()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inactivatedAt = DateTime.UtcNow.AddDays(-5);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = inactivatedAt,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = false,
                Role = UserRole.Student
            }
        };

        var studentRepository =
            Substitute.For<IStudentRepository>();

        var enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        var gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        studentRepository
            .GetByGymIdForLifecycleAsync(gymId)
            .Returns(new List<Student> { student });

        enrollmentRepository
            .GetByStudentIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                gymId)
            .Returns(new List<Enrollment>());

        var service = new StudentLifecycleService(
            studentRepository,
            enrollmentRepository,
            gymTimeZoneProvider);

        await service.ProcessAsync(gymId);

        Assert.False(student.User.IsActive);
        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);
        Assert.Equal(inactivatedAt, student.InactivatedAt);

        await studentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }
}