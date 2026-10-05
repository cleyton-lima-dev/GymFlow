using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPhysicalAccessEventRepository
{
    Task<PhysicalAccessEvent?> GetByRequestIdAsync(
        Guid gymId,
        Guid requestId);

    Task<PhysicalAccessEvent> AddAsync(
        PhysicalAccessEvent accessEvent);
}