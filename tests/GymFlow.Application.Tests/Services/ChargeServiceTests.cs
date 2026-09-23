using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using NSubstitute;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Tests.Services;

public class ChargeServiceTests
{
    private readonly IChargeRepository _chargeRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly ChargeService _service;

    public ChargeServiceTests()
    {
        _chargeRepository =
            Substitute.For<IChargeRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _service = new ChargeService(
            _chargeRepository,
            _gymTimeZoneProvider);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenChargeIsPending_ShouldMarkAsPaidAndActivateEnrollment()
    {
        var gymId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();
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

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanId = Guid.NewGuid(),
            StartDate = today,
            EndDate = today.AddMonths(1),
            Status = EnrollmentStatus.PendingPayment,
            Student = student
        };

        var charge = new Charge
        {
            Id = chargeId,
            EnrollmentId = enrollment.Id,
            Amount = 150m,
            DueDate = today,
            Status = ChargeStatus.Pending,
            Enrollment = enrollment
        };

        _chargeRepository
            .GetByIdAsync(chargeId, gymId)
            .Returns(charge);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var result =
            await _service.ConfirmPaymentAsync(
                gymId,
                chargeId);

        Assert.True(result);
        Assert.Equal(ChargeStatus.Paid, charge.Status);
        Assert.NotNull(charge.PaidAt);

        Assert.Equal(
            EnrollmentStatus.Active,
            enrollment.Status);

        Assert.True(student.User.IsActive);
        Assert.Null(student.NoValidEnrollmentSince);
        Assert.Null(student.InactivationReason);
        Assert.Null(student.InactivatedAt);

        await _chargeRepository
            .Received(1)
            .UpdateAsync(charge);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenEnrollmentStartsInFuture_ShouldNotReactivateStudentYet()
    {
        var gymId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();
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

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanId = Guid.NewGuid(),
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(1),
            Status = EnrollmentStatus.PendingPayment,
            Student = student
        };

        var charge = new Charge
        {
            Id = chargeId,
            EnrollmentId = enrollment.Id,
            Amount = 150m,
            DueDate = enrollment.StartDate,
            Status = ChargeStatus.Pending,
            Enrollment = enrollment
        };

        _chargeRepository
            .GetByIdAsync(chargeId, gymId)
            .Returns(charge);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var result =
            await _service.ConfirmPaymentAsync(
                gymId,
                chargeId);

        Assert.True(result);
        Assert.Equal(ChargeStatus.Paid, charge.Status);
        Assert.Equal(
            EnrollmentStatus.Active,
            enrollment.Status);

        Assert.False(student.User.IsActive);
        Assert.NotNull(student.NoValidEnrollmentSince);
        Assert.Equal(
            StudentInactivationReason.NoValidEnrollment,
            student.InactivationReason);
        Assert.NotNull(student.InactivatedAt);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenChargeIsAlreadyPaid_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();

        var charge = new Charge
        {
            Id = chargeId,
            Status = ChargeStatus.Paid,
            PaidAt = DateTime.UtcNow
        };

        _chargeRepository
            .GetByIdAsync(chargeId, gymId)
            .Returns(charge);

        var result =
            await _service.ConfirmPaymentAsync(
                gymId,
                chargeId);

        Assert.False(result);

        await _chargeRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Charge>());
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenChargeIsCancelled_ShouldReturnFalse()
    {
        var gymId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();

        var charge = new Charge
        {
            Id = chargeId,
            Status = ChargeStatus.Cancelled
        };

        _chargeRepository
            .GetByIdAsync(chargeId, gymId)
            .Returns(charge);

        var result =
            await _service.ConfirmPaymentAsync(
                gymId,
                chargeId);

        Assert.False(result);

        await _chargeRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Charge>());
    }

    [Fact]
    public async Task ListAsync_ShouldReturnChargesFromAuthenticatedGym()
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
                Role = UserRole.Student
            }
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanName = "Plano Mensal",
            Student = student
        };

        var charge = new Charge
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Amount = 150m,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ChargeStatus.Pending,
            Enrollment = enrollment
        };

        _chargeRepository
            .GetByGymAsync(gymId)
            .Returns(new List<Charge> { charge });

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var result =
            await _service.ListAsync(gymId);

        Assert.Single(result);

        Assert.Equal(charge.Id, result[0].Id);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Equal("Aluno Teste", result[0].StudentName);
        Assert.Equal("Plano Mensal", result[0].PlanName);
        Assert.Equal(150m, result[0].Amount);
        Assert.Equal(ChargeStatus.Pending, result[0].Status);
    }

    [Fact]
    public async Task ListAsync_WhenPendingChargeIsPastDue_ShouldReturnOverdue()
    {
        var gymId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                Role = UserRole.Student
            }
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanName = "Plano Mensal",
            Student = student
        };

        var charge = new Charge
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Amount = 150m,
            DueDate = today.AddDays(-1),
            Status = ChargeStatus.Pending,
            Enrollment = enrollment
        };

        _chargeRepository
            .GetByGymAsync(gymId)
            .Returns(new List<Charge> { charge });

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var result =
            await _service.ListAsync(gymId);

        Assert.Single(result);
        Assert.Equal(
            ChargeStatus.Overdue,
            result[0].Status);
    }
}