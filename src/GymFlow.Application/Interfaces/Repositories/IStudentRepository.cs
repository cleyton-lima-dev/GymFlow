using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IStudentRepository
{
    Task AddAsync(User user, Student student);

    Task<(List<Student> Items, int TotalCount)> GetPagedByGymIdAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    int skip,
    int take);

    Task<Student?> GetByIdAndGymIdAsync(Guid studentId, Guid gymId);

    Task UpdateAsync(Student student);

    Task<Student?> GetByUserIdAndGymIdAsync(
         Guid userId,
         Guid gymId);
}