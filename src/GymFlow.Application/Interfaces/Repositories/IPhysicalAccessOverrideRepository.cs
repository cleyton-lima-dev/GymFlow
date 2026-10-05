using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPhysicalAccessOverrideRepository
{
    Task<PhysicalAccessOverride?> GetByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task AddAsync(
        PhysicalAccessOverride accessOverride);

    Task UpdateAsync(
        PhysicalAccessOverride accessOverride);

    Task DeleteAsync(
        PhysicalAccessOverride accessOverride);
}