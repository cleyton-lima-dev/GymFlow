using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;

namespace GymFlow.Application.Services;

public class StudentAccessService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;

    public StudentAccessService(
        IStudentRepository studentRepository,
        IEnrollmentRepository enrollmentRepository,
        IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
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