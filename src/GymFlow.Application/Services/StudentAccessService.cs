using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Interfaces.Security;

namespace GymFlow.Application.Services;

public class StudentAccessService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly IGymErpPolicy _erpPolicy;

    public StudentAccessService(
        IStudentRepository studentRepository,
        IEnrollmentRepository enrollmentRepository,
        IGymTimeZoneProvider gymTimeZoneProvider,
        IGymErpPolicy erpPolicy)
    {
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
        _erpPolicy = erpPolicy;
    }

    // Identity and activity must already have been validated by the caller.
    public async Task<bool> CanAccessApplicationAsync(Guid userId, Guid gymId)
    {
        var student = await _studentRepository.GetByUserIdAndGymIdAsync(userId, gymId);
        if (student is null || student.ArchivedAt is not null ||
            !student.IsActive || !student.AppAccessEnabled ||
            student.User is null || !student.User.IsActive)
            return false;

        return !_erpPolicy.IsEnabled(gymId) ||
            await HasActiveEnrollmentForStudentAsync(student.Id, gymId);
    }

    public async Task<bool> HasActiveEnrollmentAsync(
        Guid userId,
        Guid gymId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        if (student is null)
            return false;

        return await HasActiveEnrollmentForStudentAsync(student.Id, gymId);
    }

    private async Task<bool> HasActiveEnrollmentForStudentAsync(Guid studentId, Guid gymId)
    {
        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var localNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone);

        var today =
            DateOnly.FromDateTime(localNow);

        var enrollment = await _enrollmentRepository
            .GetActiveByStudentAsync(
                studentId,
                gymId,
                today);

        return enrollment is not null;
    }
}
