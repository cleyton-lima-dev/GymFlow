using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Application.DTOs.Students;

namespace GymFlow.Application.Services;

public class EnrollmentService
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly IChargeRepository _chargeRepository;

    public EnrollmentService(
        IEnrollmentRepository enrollmentRepository,
        IStudentRepository studentRepository,
        IPlanRepository planRepository,
        IChargeRepository chargeRepository,
        IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _planRepository = planRepository;
        _chargeRepository = chargeRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
    }

    public async Task<EnrollmentResponse?> CreateAsync(
        Guid gymId,
        CreateEnrollmentRequest request)
    {
        if (request.StudentId == Guid.Empty ||
            request.PlanId == Guid.Empty ||
            request.StartDate == default)
        {
            return null;
        }

        var student = await _studentRepository
            .GetByIdAndGymIdAsync(request.StudentId, gymId);

        if (student is null ||
            student.ArchivedAt is not null)
        {
            return null;
        }

        var plan = await _planRepository
            .GetByIdAsync(request.PlanId, gymId);

        if (plan is null || !plan.IsActive)
            return null;

        var today = GetToday(gymId);

        var endDate =
            request.StartDate.AddMonths(
            plan.DurationMonths);

        var hasOverlappingEnrollment =
            await _enrollmentRepository
                .HasOverlappingEnrollmentAsync(
                    request.StudentId,
                    gymId,
                    request.StartDate,
                    endDate);

        if (hasOverlappingEnrollment)
            return null;

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PlanId = plan.Id,
            StartDate = request.StartDate,
            EndDate = endDate,

            Status = EnrollmentStatus.Active,

            PlanName = plan.Name,
            PlanPrice = plan.Price,
            PlanDurationMonths = plan.DurationMonths,
            PlanBillingCycle = plan.BillingCycle,

            CreatedAt = DateTime.UtcNow,

            Student = student,
            Plan = plan
        };

        enrollment.Charge = new Charge
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Amount = enrollment.PlanPrice,
            DueDate = enrollment.StartDate,
            Status = ChargeStatus.Paid,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _enrollmentRepository.AddAsync(enrollment);

        var isValidToday =
    enrollment.Status == EnrollmentStatus.Active &&
    enrollment.StartDate <= today &&
    enrollment.EndDate > today;

        if (isValidToday)
        {
            var studentChanged = false;

            if (student.NoValidEnrollmentSince is not null)
            {
                student.NoValidEnrollmentSince = null;
                studentChanged = true;
            }

            if (student.InactivationReason ==
                StudentInactivationReason.NoValidEnrollment)
            {
                var now = DateTime.UtcNow;

                student.User.IsActive = true;
                student.InactivationReason = null;
                student.InactivatedAt = null;

                student.User.UpdatedAt = now;
                student.UpdatedAt = now;

                studentChanged = true;
            }

            if (studentChanged)
            {
                await _studentRepository.SaveChangesAsync();
            }
        }

        return ToResponse(
            enrollment,
            GetEffectiveStatus(
                enrollment,
                GetToday(gymId)));
    }

    public async Task<bool> CancelAsync(
    Guid gymId,
    Guid enrollmentId)
    {
        var enrollment = await _enrollmentRepository
            .GetByIdAsync(enrollmentId, gymId);

        if (enrollment is null)
            return false;

        var today = GetToday(gymId);

        var effectiveStatus =
            GetEffectiveStatus(enrollment, today);

        if (effectiveStatus != EnrollmentStatus.Active &&
            effectiveStatus != EnrollmentStatus.PendingPayment)
        {
            return false;
        }

        if (effectiveStatus == EnrollmentStatus.PendingPayment &&
             enrollment.Charge is not null &&
             (enrollment.Charge.Status == ChargeStatus.Pending ||
              enrollment.Charge.Status == ChargeStatus.Overdue))
        {
            enrollment.Charge.Status = ChargeStatus.Cancelled;
            enrollment.Charge.UpdatedAt = DateTime.UtcNow;
        }

        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.CancellationDate = today;
        enrollment.UpdatedAt = DateTime.UtcNow;

        await _enrollmentRepository.UpdateAsync(enrollment);

        return true;
    }

    public async Task<EnrollmentResponse?> RenewAsync(
    Guid gymId,
    Guid enrollmentId,
    RenewEnrollmentRequest request)
    {
        if (request.PlanId == Guid.Empty)
            return null;

        var currentEnrollment = await _enrollmentRepository
            .GetByIdAsync(enrollmentId, gymId);

        if (currentEnrollment is null ||
            currentEnrollment.Student.ArchivedAt is not null)
        {
            return null;
        }

        var plan = await _planRepository
            .GetByIdAsync(request.PlanId, gymId);

        if (plan is null || !plan.IsActive)
            return null;

        var today = GetToday(gymId);

        var currentStatus =
            GetEffectiveStatus(currentEnrollment, today);

        var startDate =
            currentStatus == EnrollmentStatus.Active
                ? currentEnrollment.EndDate
                : today;

        var endDate =
            startDate.AddMonths(plan.DurationMonths);

        var hasOverlappingEnrollment =
            await _enrollmentRepository
                .HasOverlappingEnrollmentAsync(
                    currentEnrollment.StudentId,
                    gymId,
                    startDate,
                    endDate);

        if (hasOverlappingEnrollment)
            return null;

        var renewedEnrollment = new Enrollment
        {
            Id = Guid.NewGuid(),

            StudentId = currentEnrollment.StudentId,
            PlanId = plan.Id,

            StartDate = startDate,
            EndDate = endDate,

            Status = EnrollmentStatus.PendingPayment,

            PlanName = plan.Name,
            PlanPrice = plan.Price,
            PlanDurationMonths = plan.DurationMonths,
            PlanBillingCycle = plan.BillingCycle,

            CreatedAt = DateTime.UtcNow,

            Student = currentEnrollment.Student,
            Plan = plan
        };

        renewedEnrollment.Charge = new Charge
        {
            Id = Guid.NewGuid(),
            EnrollmentId = renewedEnrollment.Id,
            Amount = renewedEnrollment.PlanPrice,
            DueDate = renewedEnrollment.StartDate,
            Status = ChargeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _enrollmentRepository
            .AddAsync(renewedEnrollment);

        var student = currentEnrollment.Student;

        var isValidToday =
            renewedEnrollment.Status == EnrollmentStatus.Active &&
            renewedEnrollment.StartDate <= today &&
            renewedEnrollment.EndDate > today;

        if (isValidToday)
        {
            var studentChanged = false;

            if (student.NoValidEnrollmentSince is not null)
            {
                student.NoValidEnrollmentSince = null;
                studentChanged = true;
            }

            if (student.InactivationReason ==
                StudentInactivationReason.NoValidEnrollment)
            {
                var now = DateTime.UtcNow;

                student.User.IsActive = true;
                student.InactivationReason = null;
                student.InactivatedAt = null;

                student.User.UpdatedAt = now;
                student.UpdatedAt = now;

                studentChanged = true;
            }

            if (studentChanged)
            {
                await _studentRepository.SaveChangesAsync();
            }
        }

        return ToResponse(
            renewedEnrollment,
            GetEffectiveStatus(
                renewedEnrollment,
                today));
    }

    public async Task<EnrollmentResponse?> ReactivateArchivedStudentAsync(
    Guid gymId,
    Guid studentId,
    ReactivateArchivedStudentRequest request)
    {
        if (studentId == Guid.Empty ||
            request.PlanId == Guid.Empty ||
            request.StartDate == default)
        {
            return null;
        }

        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null ||
            student.ArchivedAt is null)
        {
            return null;
        }

        var plan = await _planRepository
            .GetByIdAsync(request.PlanId, gymId);

        if (plan is null || !plan.IsActive)
            return null;

        var today = GetToday(gymId);

        var endDate =
            request.StartDate.AddMonths(
                plan.DurationMonths);

        if (endDate <= today)
            return null;

        var hasOverlappingEnrollment =
            await _enrollmentRepository
                .HasOverlappingEnrollmentAsync(
                    student.Id,
                    gymId,
                    request.StartDate,
                    endDate);

        if (hasOverlappingEnrollment)
            return null;

        var now = DateTime.UtcNow;

        student.ArchivedAt = null;

        var isValidToday =
            request.StartDate <= today &&
            endDate > today;

        if (isValidToday)
        {
            student.User.IsActive = true;
            student.NoValidEnrollmentSince = null;
            student.InactivationReason = null;
            student.InactivatedAt = null;
        }
        else
        {
            student.User.IsActive = false;
            student.NoValidEnrollmentSince = today;
            student.InactivationReason =
                StudentInactivationReason.NoValidEnrollment;
            student.InactivatedAt = now;
        }

        student.User.UpdatedAt = now;
        student.UpdatedAt = now;

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),

            StudentId = student.Id,
            PlanId = plan.Id,

            StartDate = request.StartDate,
            EndDate = endDate,

            Status = EnrollmentStatus.Active,

            PlanName = plan.Name,
            PlanPrice = plan.Price,
            PlanDurationMonths = plan.DurationMonths,
            PlanBillingCycle = plan.BillingCycle,

            CreatedAt = now,

            Student = student,
            Plan = plan
        };

        await _enrollmentRepository
            .AddAsync(enrollment);

        await _studentRepository
            .SaveChangesAsync();

        return ToResponse(
            enrollment,
            GetEffectiveStatus(
                enrollment,
                today));
    }

    public async Task<List<EnrollmentResponse>> ListAsync(
        Guid gymId,
        EnrollmentStatus? status = null)
    {
        var enrollments = await _enrollmentRepository
            .GetByGymAsync(gymId, null);

        var today = GetToday(gymId);

        return enrollments
            .Select(enrollment => new
            {
                Enrollment = enrollment,
                EffectiveStatus =
                    GetEffectiveStatus(enrollment, today)
            })
            .Where(item =>
                !status.HasValue ||
                item.EffectiveStatus == status.Value)
            .Select(item =>
                ToResponse(
                    item.Enrollment,
                    item.EffectiveStatus))
            .ToList();
    }

    private DateOnly GetToday(Guid gymId)
    {
        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var localNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone);

        return DateOnly.FromDateTime(localNow);
    }

    private static EnrollmentStatus GetEffectiveStatus(
        Enrollment enrollment,
        DateOnly today)
    {
        if (enrollment.Status == EnrollmentStatus.Active &&
            enrollment.EndDate <= today)
        {
            return EnrollmentStatus.Expired;
        }

        return enrollment.Status;
    }

    private static EnrollmentResponse ToResponse(
        Enrollment enrollment,
        EnrollmentStatus status)
    {
        return new EnrollmentResponse
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            StudentName = enrollment.Student.User.Name,

            PlanId = enrollment.PlanId,
            PlanName = enrollment.PlanName,
            PlanPrice = enrollment.PlanPrice,
            PlanDurationMonths =
                enrollment.PlanDurationMonths,
            PlanBillingCycle =
                enrollment.PlanBillingCycle,

            StartDate = enrollment.StartDate,
            EndDate = enrollment.EndDate,
            Status = status,
            CancellationDate =
                enrollment.CancellationDate,

            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt
        };
    }
}