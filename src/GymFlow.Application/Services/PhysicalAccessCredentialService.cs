using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using System.Security.Cryptography;
using System.Globalization;

namespace GymFlow.Application.Services;

public class PhysicalAccessCredentialService
{
    private readonly IPhysicalAccessCredentialRepository
        _credentialRepository;

    private readonly IStudentRepository
        _studentRepository;

    private readonly IPhysicalAccessPinProtector _pinProtector;

    public const string PinProviderKey = "toletus-litenet2";

    public PhysicalAccessCredentialService(
        IPhysicalAccessCredentialRepository credentialRepository,
        IStudentRepository studentRepository,
        IPhysicalAccessPinProtector pinProtector)
    {
        _credentialRepository = credentialRepository;
        _studentRepository = studentRepository;
        _pinProtector = pinProtector;
    }

    public async Task<PhysicalAccessCredentialResponse?> CreateAsync(
        Guid gymId,
        CreatePhysicalAccessCredentialRequest request)
    {
        if (request.StudentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ProviderKey) ||
            string.IsNullOrWhiteSpace(request.ExternalIdentifier) ||
            !Enum.IsDefined(request.Type) ||
            request.Type == PhysicalAccessCredentialType.PinExternalId)
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

    public async Task<IssuedPhysicalAccessPinResponse?> IssuePinAsync(
        Guid gymId,
        Guid studentId)
    {
        if (gymId == Guid.Empty || studentId == Guid.Empty)
            return null;

        var student = await _studentRepository.GetByIdAndGymIdAsync(
            studentId, gymId);
        if (student is null || !student.IsActive || student.ArchivedAt is not null)
            return null;

        var pins = (await _credentialRepository.GetByStudentAsync(studentId, gymId))
            .Where(credential => credential.Type == PhysicalAccessCredentialType.PinExternalId)
            .ToList();

        // Multiple historical PIN rows have ambiguous revocation semantics.
        if (pins.Count > 1)
            return null;

        var existing = pins.SingleOrDefault();

        var start = RandomNumberGenerator.GetInt32(10000);
        int step;
        do
        {
            step = RandomNumberGenerator.GetInt32(1, 10000);
        }
        while (step % 2 == 0 || step % 5 == 0);

        // The coprime step visits every four-digit value exactly once.
        for (var attempt = 0; attempt < 10000; attempt++)
        {
            var pin = ((start + attempt * step) % 10000)
                .ToString("D4", CultureInfo.InvariantCulture);
            var lookupHash = _pinProtector.ComputeLookupHash(gymId, pin);
            var collision = await _credentialRepository.GetByExternalIdentifierAsync(
                gymId, PinProviderKey, PhysicalAccessCredentialType.PinExternalId,
                lookupHash);

            if (collision is not null)
                continue;

            if (existing is null)
            {
                existing = new PhysicalAccessCredential
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    StudentId = studentId,
                    Type = PhysicalAccessCredentialType.PinExternalId,
                    ProviderKey = PinProviderKey,
                    ExternalIdentifier = lookupHash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Student = student
                };
                await _credentialRepository.AddAsync(existing);
            }
            else
            {
                if (existing.ExternalIdentifier == lookupHash)
                    continue;

                existing.ProviderKey = PinProviderKey;
                existing.ExternalIdentifier = lookupHash;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
                await _credentialRepository.UpdateAsync(existing);
            }

            return new IssuedPhysicalAccessPinResponse(ToResponse(existing), pin);
        }

        return null;
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
                credential.Type == PhysicalAccessCredentialType.PinExternalId
                    ? string.Empty
                    : credential.ExternalIdentifier,
            IsActive = credential.IsActive,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt
        };
    }
}
