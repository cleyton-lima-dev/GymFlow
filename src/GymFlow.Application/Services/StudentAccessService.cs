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
    public Task<bool> CanAccessApplicationAsync(Guid userId, Guid gymId) =>
        _erpPolicy.IsEnabled(gymId)
            ? HasActiveEnrollmentAsync(userId, gymId)
            : Task.FromResult(true);

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
                student.Id,
                gymId,
                today);

        return enrollment is not null;
    }
}
