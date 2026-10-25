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

    Task<List<Charge>> GetPaidByGymAndPeriodAsync(
        Guid gymId,
        DateTime fromUtc,
        DateTime untilUtc);

    Task UpdateAsync(Charge charge);
}
