using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IStudentRepository
{
    Task AddAsync(User user, Student student);

    Task<List<Student>> GetAllByGymIdAsync(Guid gymId);

    Task<Student?> GetByIdAndGymIdAsync(Guid studentId, Guid gymId);

    Task UpdateAsync(Student student);

    Task<Student?> GetByUserIdAndGymIdAsync(
         Guid userId,
         Guid gymId);
}