using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class StudentLifecycleService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;

    public StudentLifecycleService(
        IStudentRepository studentRepository,
        IEnrollmentRepository enrollmentRepository,
        IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
    }

    public async Task ProcessAsync(Guid gymId)
    {
        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var localNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone);

        var today =
            DateOnly.FromDateTime(localNow);

        var students =
            await _studentRepository
                .GetByGymIdForLifecycleAsync(gymId);

        if (students.Count == 0)
            return;

        var studentIds = students
            .Select(student => student.Id)
            .ToArray();

        var enrollments =
            await _enrollmentRepository.GetByStudentIdsAsync(
                studentIds,
                gymId);

        var validStudentIds = enrollments
            .Where(enrollment =>
                enrollment.Status == EnrollmentStatus.Active &&
                enrollment.StartDate <= today &&
                enrollment.EndDate > today)
            .Select(enrollment => enrollment.StudentId)
            .ToHashSet();

        var now = DateTime.UtcNow;
        var hasChanges = false;

        foreach (var student in students)
        {
            if (validStudentIds.Contains(student.Id))
            {
                if (student.NoValidEnrollmentSince is not null)
                {
                    student.NoValidEnrollmentSince = null;
                    hasChanges = true;
                }

                if (student.InactivationReason ==
                    StudentInactivationReason.NoValidEnrollment)
                {
                    student.User.IsActive = true;
                    student.InactivationReason = null;
                    student.InactivatedAt = null;

                    student.User.UpdatedAt = now;
                    student.UpdatedAt = now;

                    hasChanges = true;
                }

                continue;
            }

            if (student.NoValidEnrollmentSince is null)
            {
                var lastLossOfValidEnrollment =
                    enrollments
                        .Where(enrollment =>
                            enrollment.StudentId == student.Id)
                        .Select(enrollment =>
                        {
                            if (enrollment.Status ==
                                    EnrollmentStatus.Active &&
                                enrollment.EndDate <= today)
                            {
                                return (DateOnly?)enrollment.EndDate;
                            }

                            if (enrollment.Status ==
                                    EnrollmentStatus.Cancelled &&
                                enrollment.CancellationDate.HasValue &&
                                enrollment.StartDate <=
                                    enrollment.CancellationDate.Value &&
                                enrollment.CancellationDate.Value <= today)
                            {
                                return enrollment.CancellationDate.Value;
                            }

                            return null;
                        })
                        .Where(date => date.HasValue)
                        .Select(date => date!.Value)
                        .DefaultIfEmpty(today)
                        .Max();

                student.NoValidEnrollmentSince =
                    lastLossOfValidEnrollment;

                student.UpdatedAt = now;
                hasChanges = true;

            }

            var daysWithoutValidEnrollment =
                today.DayNumber -
                student.NoValidEnrollmentSince.Value.DayNumber;

            if (daysWithoutValidEnrollment >= 90)
            {
                if (student.User.IsActive)
                {
                    student.User.IsActive = false;
                    student.InactivationReason =
                        StudentInactivationReason.NoValidEnrollment;
                    student.InactivatedAt ??= now;
                    student.User.UpdatedAt = now;
                }

                student.ArchivedAt ??= now;
                student.UpdatedAt = now;

                hasChanges = true;

                continue;
            }

            if (daysWithoutValidEnrollment >= 30 &&
                student.User.IsActive)
            {
                student.User.IsActive = false;
                student.InactivationReason =
                    StudentInactivationReason.NoValidEnrollment;
                student.InactivatedAt = now;

                student.User.UpdatedAt = now;
                student.UpdatedAt = now;

                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _studentRepository.SaveChangesAsync();
        }
    }
}