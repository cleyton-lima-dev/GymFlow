using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IEnrollmentRepository
{
    Task AddAsync(Enrollment enrollment);

    Task<Enrollment?> GetByIdAsync(
        Guid enrollmentId,
        Guid gymId);

    Task<Enrollment?> GetActiveByStudentAsync(
        Guid studentId,
        Guid gymId,
        DateOnly referenceDate);

    Task<List<Enrollment>> GetByGymAsync(
        Guid gymId,
        EnrollmentStatus? status);

    Task UpdateAsync(Enrollment enrollment);

    Task<bool> HasOverlappingEnrollmentAsync(
    Guid studentId,
    Guid gymId,
    DateOnly startDate,
    DateOnly endDate);

    Task<List<Enrollment>> GetByStudentIdsAsync(
    IReadOnlyCollection<Guid> studentIds,
    Guid gymId);
}