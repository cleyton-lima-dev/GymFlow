using GymFlow.Domain.Enums;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPhysicalAccessEventRepository
{
    Task<PhysicalAccessEvent?> GetByRequestIdAsync(
        Guid gymId,
        Guid requestId);

    Task<PhysicalAccessEvent> AddAsync(
        PhysicalAccessEvent accessEvent);

    Task<(List<PhysicalAccessEvent> Items, int TotalCount)> GetPagedAsync(
        Guid gymId,
        Guid? studentId,
        PhysicalAccessDecision? decision,
        PhysicalAccessDecisionSource? source,
        DateTime? occurredFrom,
        DateTime? occurredTo,
        int skip,
        int take);
}
