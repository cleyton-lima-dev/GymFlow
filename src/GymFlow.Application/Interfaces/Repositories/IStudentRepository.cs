using GymFlow.Domain.Entities;
using GymFlow.Application.DTOs.Students;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IStudentRepository
{
    Task AddAsync(User user, Student student);

    Task<(List<Student> Items, int TotalCount)> GetPagedByGymIdAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    StudentEnrollmentFilter? enrollmentFilter,
    StudentArchiveFilter archiveFilter,
    DateOnly referenceDate,
    int skip,
    int take);

    Task<Student?> GetByIdAndGymIdAsync(Guid studentId, Guid gymId);

    Task<List<Guid>> GetGymIdsForLifecycleAsync();

    Task<List<Student>> GetByGymIdForLifecycleAsync(Guid gymId);

    Task UpdateAsync(Student student);

    Task<Student?> GetByUserIdAndGymIdAsync(
         Guid userId,
         Guid gymId);

    Task SaveChangesAsync();

}