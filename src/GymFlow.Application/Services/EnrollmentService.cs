using GymFlow.Application.DTOs.Enrollments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class EnrollmentService
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;

    public EnrollmentService(
        IEnrollmentRepository enrollmentRepository,
        IStudentRepository studentRepository,
        IPlanRepository planRepository,
        IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _planRepository = planRepository;
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

        if (student is null || !student.User.IsActive)
            return null;

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

        await _enrollmentRepository.AddAsync(enrollment);

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

        if (effectiveStatus != EnrollmentStatus.Active)
            return false;

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

        if (currentEnrollment is null)
            return null;

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

            Status = EnrollmentStatus.Active,

            PlanName = plan.Name,
            PlanPrice = plan.Price,
            PlanDurationMonths = plan.DurationMonths,
            PlanBillingCycle = plan.BillingCycle,

            CreatedAt = DateTime.UtcNow,

            Student = currentEnrollment.Student,
            Plan = plan
        };

        await _enrollmentRepository
            .AddAsync(renewedEnrollment);

        return ToResponse(
            renewedEnrollment,
            GetEffectiveStatus(
                renewedEnrollment,
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