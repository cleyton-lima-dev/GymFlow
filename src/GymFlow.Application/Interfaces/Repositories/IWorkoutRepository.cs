using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IWorkoutRepository
{
    Task AddAsync(Workout workout);

    Task<Workout?> GetByIdAsync(
        Guid id,
        Guid gymId);

    Task<Workout?> GetActiveByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task<Workout?> GetForUpdateAsync(
        Guid id,
        Guid gymId);

    Task<Workout?> GetActiveForUpdateAsync(
        Guid studentId,
        Guid gymId);

    Task<List<Workout>> GetAllByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task SaveChangesAsync();
}