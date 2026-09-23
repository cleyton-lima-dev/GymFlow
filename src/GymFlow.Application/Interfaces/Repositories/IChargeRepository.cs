using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IChargeRepository
{
    Task AddAsync(Charge charge);

    Task<Charge?> GetByIdAsync(
        Guid chargeId,
        Guid gymId);

    Task<Charge?> GetByEnrollmentIdAsync(
        Guid enrollmentId,
        Guid gymId);

    Task<List<Charge>> GetByGymAsync(
        Guid gymId);

    Task UpdateAsync(Charge charge);
}