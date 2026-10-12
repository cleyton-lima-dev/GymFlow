using GymFlow.Domain.Entities;
namespace GymFlow.Application.Interfaces.Repositories;

public interface IGymSettingsRepository
{
    GymSettings? Get(Guid gymId);
    Task<GymSettings?> GetAsync(Guid gymId);
    Task SaveAsync(GymSettings settings, long? expectedVersion, GymSettingsAuditLog audit);
}
