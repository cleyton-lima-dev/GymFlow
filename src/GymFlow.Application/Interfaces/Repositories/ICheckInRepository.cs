using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface ICheckInRepository
{
    Task<CheckIn?> GetByRequestIdAsync(
        Guid gymId,
        Guid requestId);

    Task<CheckIn?> GetByPhysicalAccessEventIdAsync(
        Guid gymId,
        Guid physicalAccessEventId);

    Task<CheckIn> AddAsync(
        CheckIn checkIn);

    Task<(List<CheckIn> Items, int TotalCount)> GetPagedAsync(
        Guid gymId,
        Guid? studentId,
        DateTime? occurredFrom,
        DateTime? occurredTo,
        int skip,
        int take);
}