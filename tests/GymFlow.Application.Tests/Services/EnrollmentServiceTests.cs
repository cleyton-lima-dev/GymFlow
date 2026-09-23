using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using NSubstitute;
using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Application.DTOs.Students;

namespace GymFlow.Application.Tests.Services;

public class EnrollmentServiceTests
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly EnrollmentService _service;
    private readonly IChargeRepository _chargeRepository;

    public EnrollmentServiceTests()
    {
        _enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _planRepository =
            Substitute.For<IPlanRepository>();

        _chargeRepository =
            Substitute.For<IChargeRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _service = new EnrollmentService(
            _enrollmentRepository,
            _studentRepository,
            _planRepository,
            _chargeRepository,
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
            PlanBillingCycle.Monthly &&
                    enrollment.Charge != null &&
                    enrollment.Charge.EnrollmentId == enrollment.Id &&
                    enrollment.Charge.Amount == 150m &&
                    enrollment.Charge.DueDate ==
                        new DateOnly(2026, 9, 16) &&
                    enrollment.Charge.Status == ChargeStatus.Paid &&
                    enrollment.Charge.PaidAt != null));
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

        var paidAt = DateTime.UtcNow.AddDays(-5);

        enrollment.Charge = new Charge
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Amount = 150m,
            DueDate = enrollment.StartDate,
            Status = ChargeStatus.Paid,
            PaidAt = paidAt,
            Enrollment = enrollment
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
        Assert.Equal(
             ChargeStatus.Paid,
             enrollment.Charge.Status);

        Assert.Equal(
            paidAt,
            enrollment.Charge.PaidAt);

        await _enrollmentRepository
            .Received(1)
            .UpdateAsync(enrollment);
    }

    [Fact]
    public async Task CancelAsync_WhenEnrollmentIsPendingPayment_ShouldCancelEnrollmentAndCharge()
    {
        var gymId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var enrollment = new Enrollment
        {
            Id = enrollmentId,
            StudentId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(1),
            Status = EnrollmentStatus.PendingPayment
        };

        enrollment.Charge = new Charge
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Amount = 150m,
            DueDate = enrollment.StartDate,
            Status = ChargeStatus.Pending,
            Enrollment = enrollment
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

        Assert.Equal(
            ChargeStatus.Cancelled,
            enrollment.Charge.Status);

        Assert.NotNull(enrollment.Charge.UpdatedAt);
        Assert.NotNull(enrollment.CancellationDate);

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
                    enrollment.StartDate == currentEnrollment.EndDate &&
                    enrollment.EndDate ==
                    currentEnrollment.EndDate.AddMonths(
                        newPlan.DurationMonths) &&
                    enrollment.Status ==
                EnrollmentStatus.PendingPayment &&
                    enrollment.PlanName == "Plano Anual" &&
                    enrollment.PlanPrice == 1200m &&
                    enrollment.PlanBillingCycle ==
                PlanBillingCycle.Annual &&
                    enrollment.Charge != null &&
                    enrollment.Charge.EnrollmentId ==
                    enrollment.Id &&
                    enrollment.Charge.Amount == 1200m &&
                    enrollment.Charge.DueDate ==
                        currentEnrollment.EndDate &&
                    enrollment.Charge.Status ==
                ChargeStatus.Pending &&
                    enrollment.Charge.PaidAt == null));
    }

    [Fact]
    public async Task CreateAsync_WhenStudentWasAutomaticallyInactive_ShouldCreateEnrollmentAndReactivate()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-10),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = false,
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
            StartDate = today
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
                today,
                today.AddMonths(1))
            .Returns(false);

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.NotNull(result);

        Assert.True(student.User.IsActive);
        Assert.Null(student.NoValidEnrollmentSince);
        Assert.Null(student.InactivationReason);
        Assert.Null(student.InactivatedAt);
    }

    [Fact]
    public async Task CreateAsync_WhenStudentIsManuallyInactive_ShouldCreateEnrollmentButRemainInactive()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason = StudentInactivationReason.Manual,
            InactivatedAt = DateTime.UtcNow.AddDays(-10),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = false,
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
            StartDate = today
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
                today,
                today.AddMonths(1))
            .Returns(false);

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.NotNull(result);
        Assert.False(student.User.IsActive);
        Assert.Null(student.NoValidEnrollmentSince);

        Assert.Equal(
            StudentInactivationReason.Manual,
            student.InactivationReason);

        Assert.NotNull(student.InactivatedAt);
    }

    [Fact]
    public async Task CreateAsync_WhenEnrollmentStartsInFuture_ShouldNotReactivateStudentYet()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureStartDate = today.AddDays(5);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-10),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = false,
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
            StartDate = futureStartDate
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
                futureStartDate,
                futureStartDate.AddMonths(1))
            .Returns(false);

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.NotNull(result);
        Assert.False(student.User.IsActive);

        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);

        Assert.NotNull(student.NoValidEnrollmentSince);
        Assert.NotNull(student.InactivatedAt);
    }

    [Fact]
    public async Task RenewAsync_WhenAutomaticallyInactiveStudentRenewsExpiredEnrollment_ShouldRemainInactiveUntilPayment()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            NoValidEnrollmentSince = today.AddDays(-40),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-10),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = false,
                Role = UserRole.Student
            }
        };

        var currentEnrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanId = Guid.NewGuid(),
            StartDate = today.AddMonths(-2),
            EndDate = today.AddMonths(-1),
            Status = EnrollmentStatus.Active,
            Student = student
        };

        var newPlan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Plano Mensal",
            Price = 150m,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly,
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
                today,
                today.AddMonths(1))
            .Returns(false);

        var result =
            await _service.RenewAsync(
                gymId,
                currentEnrollment.Id,
                request);

        Assert.NotNull(result);
        Assert.False(student.User.IsActive);

        Assert.NotNull(student.NoValidEnrollmentSince);

        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);

        Assert.NotNull(student.InactivatedAt);
    }

    [Fact]
    public async Task RenewAsync_WhenStudentIsArchived_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            ArchivedAt = DateTime.UtcNow.AddDays(-1),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Arquivado",
                IsActive = false,
                Role = UserRole.Student
            }
        };

        var currentEnrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanId = Guid.NewGuid(),
            StartDate = today.AddMonths(-2),
            EndDate = today.AddMonths(-1),
            Status = EnrollmentStatus.Active,
            Student = student
        };

        var newPlan = new Plan
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Plano Mensal",
            Price = 150m,
            DurationMonths = 1,
            BillingCycle = PlanBillingCycle.Monthly,
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

        var result =
            await _service.RenewAsync(
                gymId,
                currentEnrollment.Id,
                request);

        Assert.Null(result);

        await _enrollmentRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>());
    }

    [Fact]
    public async Task ReactivateArchivedStudentAsync_WhenEnrollmentStartsToday_ShouldUnarchiveAndActivateStudent()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            ArchivedAt = DateTime.UtcNow.AddDays(-10),
            NoValidEnrollmentSince = today.AddDays(-100),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-70),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Arquivado",
                IsActive = false,
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

        var request = new ReactivateArchivedStudentRequest
        {
            PlanId = plan.Id,
            StartDate = today
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
                today,
                today.AddMonths(1))
            .Returns(false);

        var result =
            await _service.ReactivateArchivedStudentAsync(
                gymId,
                student.Id,
                request);

        Assert.NotNull(result);

        Assert.Null(student.ArchivedAt);
        Assert.True(student.User.IsActive);
        Assert.Null(student.NoValidEnrollmentSince);
        Assert.Null(student.InactivationReason);
        Assert.Null(student.InactivatedAt);

        await _enrollmentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<Enrollment>(enrollment =>
                    enrollment.StudentId == student.Id &&
                    enrollment.PlanId == plan.Id &&
                    enrollment.StartDate == today &&
                    enrollment.EndDate == today.AddMonths(1)));
    }

    [Fact]
    public async Task ReactivateArchivedStudentAsync_WhenEnrollmentStartsInFuture_ShouldUnarchiveButRemainInactive()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(10);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            ArchivedAt = DateTime.UtcNow.AddDays(-10),
            NoValidEnrollmentSince = today.AddDays(-100),
            InactivationReason =
                StudentInactivationReason.NoValidEnrollment,
            InactivatedAt = DateTime.UtcNow.AddDays(-70),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Arquivado",
                IsActive = false,
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

        var request = new ReactivateArchivedStudentRequest
        {
            PlanId = plan.Id,
            StartDate = startDate
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
                startDate,
                startDate.AddMonths(1))
            .Returns(false);

        var result =
            await _service.ReactivateArchivedStudentAsync(
                gymId,
                student.Id,
                request);

        Assert.NotNull(result);

        Assert.Null(student.ArchivedAt);
        Assert.False(student.User.IsActive);
        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);
        Assert.NotNull(student.InactivatedAt);
        Assert.Equal(
            today,
            student.NoValidEnrollmentSince);
    }

    [Fact]
    public async Task CreateAsync_WhenStudentIsArchived_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();

        var student = new Student
        {
            Id = Guid.NewGuid(),
            ArchivedAt = DateTime.UtcNow,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Arquivado",
                IsActive = false,
                Role = UserRole.Student
            }
        };

        var request = new CreateEnrollmentRequest
        {
            StudentId = student.Id,
            PlanId = Guid.NewGuid(),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        var result =
            await _service.CreateAsync(
                gymId,
                request);

        Assert.Null(result);

        await _enrollmentRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>());
    }
}