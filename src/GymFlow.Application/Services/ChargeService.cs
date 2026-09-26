using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Enums;
using GymFlow.Application.DTOs.Charges;
using System.Text.Json;
using GymFlow.Domain.Entities;


namespace GymFlow.Application.Services;

public class ChargeService
{
    private readonly IChargeRepository _chargeRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly IFinancialAuditRepository _financialAuditRepository;

    public ChargeService(
    IChargeRepository chargeRepository,
    IGymTimeZoneProvider gymTimeZoneProvider,
    IFinancialAuditRepository financialAuditRepository)
    {
        _chargeRepository = chargeRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
        _financialAuditRepository = financialAuditRepository;
    }

    public async Task<List<ChargeResponse>> ListAsync(Guid gymId)
    {
        var charges = await _chargeRepository
            .GetByGymAsync(gymId);
        var now = DateTime.UtcNow;

        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        return charges
            .Select(charge => new ChargeResponse
            {
                Id = charge.Id,
                EnrollmentId = charge.EnrollmentId,
                StudentId = charge.Enrollment.StudentId,
                StudentName = charge.Enrollment.Student.User.Name,
                PlanName = charge.Enrollment.PlanName,
                Amount = charge.Amount,
                DueDate = charge.DueDate,
                Status = GetEffectiveStatus(
                        charge.Status,
                        charge.DueDate,
                        today),
                PaidAt = charge.PaidAt,
                CreatedAt = charge.CreatedAt,
                UpdatedAt = charge.UpdatedAt
            })
            .ToList();
    }

    public async Task<bool> ConfirmPaymentAsync(
    Guid gymId,
    Guid actorUserId,
    Guid chargeId)
    {
        var charge = await _chargeRepository
            .GetByIdAsync(chargeId, gymId);

        if (charge is null ||
            charge.Status == ChargeStatus.Paid ||
            charge.Status == ChargeStatus.Cancelled)
        {
            return false;
        }

        var previousValues = JsonSerializer.Serialize(new
        {
            Status = charge.Status.ToString(),
            charge.Amount,
            charge.PaidAt
        });

        var now = DateTime.UtcNow;

        charge.Status = ChargeStatus.Paid;
        charge.PaidAt = now;
        charge.UpdatedAt = now;

        charge.Enrollment.Status = EnrollmentStatus.Active;
        charge.Enrollment.UpdatedAt = now;

        var timeZone = _gymTimeZoneProvider.GetTimeZone(gymId);

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        var student = charge.Enrollment.Student;

        var isValidToday =
            charge.Enrollment.StartDate <= today &&
            charge.Enrollment.EndDate > today;

        if (isValidToday &&
            student.InactivationReason ==
                StudentInactivationReason.NoValidEnrollment)
        {
            student.User.IsActive = true;
            student.NoValidEnrollmentSince = null;
            student.InactivationReason = null;
            student.InactivatedAt = null;
            student.User.UpdatedAt = now;
            student.UpdatedAt = now;
        }

        var newValues = JsonSerializer.Serialize(new
        {
            Status = charge.Status.ToString(),
            charge.Amount,
            charge.PaidAt
        });

        var auditLog = new FinancialAuditLog
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            ActorUserId = actorUserId,
            EntityType = nameof(Charge),
            EntityId = charge.Id,
            Action = FinancialAuditAction.PaymentConfirmed,
            PreviousValues = previousValues,
            NewValues = newValues,
            OccurredAt = now
        };

        await _financialAuditRepository.StageAsync(auditLog);

        await _chargeRepository.UpdateAsync(charge);

        return true;
    }

    private static ChargeStatus GetEffectiveStatus(
    ChargeStatus status,
    DateOnly dueDate,
    DateOnly today)
    {
        if (status == ChargeStatus.Pending &&
            dueDate < today)
        {
            return ChargeStatus.Overdue;
        }

        return status;
    }
}