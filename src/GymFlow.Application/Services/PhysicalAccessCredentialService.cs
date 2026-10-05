using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class PhysicalAccessCredentialService
{
    private readonly IPhysicalAccessCredentialRepository
        _credentialRepository;

    private readonly IStudentRepository
        _studentRepository;

    public PhysicalAccessCredentialService(
        IPhysicalAccessCredentialRepository credentialRepository,
        IStudentRepository studentRepository)
    {
        _credentialRepository = credentialRepository;
        _studentRepository = studentRepository;
    }

    public async Task<PhysicalAccessCredentialResponse?> CreateAsync(
        Guid gymId,
        CreatePhysicalAccessCredentialRequest request)
    {
        if (request.StudentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ProviderKey) ||
            string.IsNullOrWhiteSpace(request.ExternalIdentifier) ||
            !Enum.IsDefined(request.Type))
        {
            return null;
        }

        var providerKey =
            request.ProviderKey.Trim();

        var externalIdentifier =
            request.ExternalIdentifier.Trim();

        PersistenceTextPolicy.ValidateMaxLength(
            providerKey,
            PersistenceTextPolicy
                .PhysicalAccessProviderKeyMaxLength,
            "O identificador do provedor");

        PersistenceTextPolicy.ValidateMaxLength(
            externalIdentifier,
            PersistenceTextPolicy
                .PhysicalAccessExternalIdentifierMaxLength,
            "O identificador externo");

        var student =
            await _studentRepository
                .GetByIdAndGymIdAsync(
                    request.StudentId,
                    gymId);

        if (student is null ||
            student.ArchivedAt is not null)
        {
            return null;
        }

        var existingCredential =
            await _credentialRepository
                .GetByExternalIdentifierAsync(
                    gymId,
                    providerKey,
                    request.Type,
                    externalIdentifier);

        if (existingCredential is not null)
        {
            return null;
        }

        var credential =
            new PhysicalAccessCredential
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = student.Id,
                Type = request.Type,
                ProviderKey = providerKey,
                ExternalIdentifier = externalIdentifier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Student = student
            };

        await _credentialRepository
            .AddAsync(credential);

        return ToResponse(credential);
    }

    public async Task<List<PhysicalAccessCredentialResponse>>
        ListByStudentAsync(
            Guid gymId,
            Guid studentId)
    {
        var student =
            await _studentRepository
                .GetByIdAndGymIdAsync(
                    studentId,
                    gymId);

        if (student is null)
        {
            return [];
        }

        var credentials =
            await _credentialRepository
                .GetByStudentAsync(
                    studentId,
                    gymId);

        return credentials
            .Select(ToResponse)
            .ToList();
    }

    public async Task<bool> UpdateStatusAsync(
        Guid gymId,
        Guid credentialId,
        bool isActive)
    {
        var credential =
            await _credentialRepository
                .GetByIdAsync(
                    credentialId,
                    gymId);

        if (credential is null)
        {
            return false;
        }

        credential.IsActive = isActive;
        credential.UpdatedAt = DateTime.UtcNow;

        await _credentialRepository
            .UpdateAsync(credential);

        return true;
    }

    private static PhysicalAccessCredentialResponse ToResponse(
        PhysicalAccessCredential credential)
    {
        return new PhysicalAccessCredentialResponse
        {
            Id = credential.Id,
            StudentId = credential.StudentId,
            Type = credential.Type,
            ProviderKey = credential.ProviderKey,
            ExternalIdentifier =
                credential.ExternalIdentifier,
            IsActive = credential.IsActive,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt
        };
    }
}