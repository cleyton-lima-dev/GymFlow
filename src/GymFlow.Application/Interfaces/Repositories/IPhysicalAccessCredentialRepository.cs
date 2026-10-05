using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPhysicalAccessCredentialRepository
{
    Task<PhysicalAccessCredential?> GetByExternalIdentifierAsync(
        Guid gymId,
        string providerKey,
        PhysicalAccessCredentialType type,
        string externalIdentifier);

    Task<PhysicalAccessCredential?> GetByIdAsync(
        Guid credentialId,
        Guid gymId);

    Task<List<PhysicalAccessCredential>> GetByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task AddAsync(PhysicalAccessCredential credential);

    Task UpdateAsync(PhysicalAccessCredential credential);
}