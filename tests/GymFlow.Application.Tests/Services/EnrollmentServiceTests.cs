using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using NSubstitute;
using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Tests.Services;

public class EnrollmentServiceTests
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly EnrollmentService _service;

    public EnrollmentServiceTests()
    {
        _enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _planRepository =
            Substitute.For<IPlanRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _service = new EnrollmentService(
            _enrollmentRepository,
            _studentRepository,
            _planRepository,
            _gymTimeZoneProvider);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateEnrollmentWithPlanSnapshot()
    {
        var gymId = Guid.NewGuid();

        var student = new Student
        {
            Id = Guid.NewGuid(),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Plano Mensal",
            Price = 150m,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly,
            IsActive = true
        };

        var request = new CreateEnrollmentRequest
        {
            StudentId = student.Id,
            PlanId = plan.Id,
            StartDate = new DateOnly(2026, 9, 16)
        };

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        _planRepository
            .GetByIdAsync(plan.Id, gymId)
            .Returns(plan);

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
            await _service.CreateAsync(
                gymId,
                request);

        Assert.NotNull(result);

        Assert.Equal(student.Id, result.StudentId);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal("Plano Mensal", result.PlanName);
        Assert.Equal(150m, result.PlanPrice);
        Assert.Equal(1, result.PlanDurationMonths);
        Assert.Equal(
            PlanBillingCycle.Monthly,
            result.PlanBillingCycle);

        Assert.Equal(
            new DateOnly(2026, 9, 16),
            result.StartDate);

        Assert.Equal(
            new DateOnly(2026, 10, 16),
            result.EndDate);

        await _enrollmentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<Enrollment>(enrollment =>
                    enrollment.StudentId == student.Id &&
                    enrollment.PlanId == plan.Id &&
                    enrollment.PlanName == "Plano Mensal" &&
                    enrollment.PlanPrice == 150m &&
                    enrollment.PlanDurationMonths == 1 &&
                    enrollment.PlanBillingCycle ==
                        PlanBillingCycle.Monthly));
    }

    [Fact]
    public async Task CreateAsync_WhenStudentAlreadyHasActiveEnrollment_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();

        var student = new Student
        {
            Id = Guid.NewGuid(),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Plano Mensal",
            Price = 150m,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly,
            IsActive = true
        };

        var request = new CreateEnrollmentRequest
        {
            StudentId = student.Id,
            PlanId = plan.Id,
            StartDate = new DateOnly(2026, 9, 16)
        };

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        _planRepository
            .GetByIdAsync(plan.Id, gymId)
            .Returns(plan);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .HasOverlappingEnrollmentAsync(
                student.Id,
                gymId,
                request.StartDate,
                request.StartDate.AddMonths(plan.DurationMonths))
            .Returns(true);

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.Null(result);

        await _enrollmentRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>());
    }

    [Fact]
    public async Task CancelAsync_WhenEnrollmentIsActive_ShouldCancelEnrollment()
    {
        var gymId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();

        var enrollment = new Enrollment
        {
            Id = enrollmentId,
            StudentId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2099, 10, 1),
            Status = EnrollmentStatus.Active
        };

        _enrollmentRepository
            .GetByIdAsync(enrollmentId, gymId)
            .Returns(enrollment);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var result =
            await _service.CancelAsync(
                gymId,
                enrollmentId);

        Assert.True(result);

        Assert.Equal(
            EnrollmentStatus.Cancelled,
            enrollment.Status);

        Assert.NotNull(enrollment.CancellationDate);
        Assert.NotNull(enrollment.UpdatedAt);

        await _enrollmentRepository
            .Received(1)
            .UpdateAsync(enrollment);
    }

    [Fact]
    public async Task RenewAsync_WhenEnrollmentIsActive_ShouldStartNewEnrollmentAfterCurrentEndDate()
    {
        var gymId = Guid.NewGuid();

        var student = new Student
        {
            Id = Guid.NewGuid(),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var currentEnrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanId = Guid.NewGuid(),
            StartDate = new DateOnly(2099, 9, 1),
            EndDate = new DateOnly(2099, 10, 1),
            Status = EnrollmentStatus.Active,
            Student = student
        };

        var newPlan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Plano Anual",
            Price = 1200m,
            DurationMonths = 12,
            BillingCycle = PlanBillingCycle.Annual,
            IsActive = true
        };

        var request = new RenewEnrollmentRequest
        {
            PlanId = newPlan.Id
        };

        _enrollmentRepository
            .GetByIdAsync(currentEnrollment.Id, gymId)
            .Returns(currentEnrollment);

        _planRepository
            .GetByIdAsync(newPlan.Id, gymId)
            .Returns(newPlan);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .HasOverlappingEnrollmentAsync(
                student.Id,
                gymId,
                currentEnrollment.EndDate,
                currentEnrollment.EndDate.AddMonths(
                newPlan.DurationMonths))
            .Returns(false);

        var result =
            await _service.RenewAsync(
                gymId,
                currentEnrollment.Id,
                request);

        Assert.NotNull(result);

        Assert.Equal(
            new DateOnly(2099, 10, 1),
            result.StartDate);

        Assert.Equal(
            new DateOnly(2100, 10, 1),
            result.EndDate);

        Assert.Equal("Plano Anual", result.PlanName);
        Assert.Equal(1200m, result.PlanPrice);

        await _enrollmentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<Enrollment>(enrollment =>
                    enrollment.StudentId == student.Id &&
                    enrollment.PlanId == newPlan.Id &&
                    enrollment.StartDate ==
                        currentEnrollment.EndDate &&
                    enrollment.PlanName == "Plano Anual" &&
                    enrollment.PlanPrice == 1200m));
    }
}