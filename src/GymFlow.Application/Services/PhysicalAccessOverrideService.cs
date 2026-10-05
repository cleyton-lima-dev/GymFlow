using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class PhysicalAccessOverrideService
{
    private readonly IPhysicalAccessOverrideRepository
        _overrideRepository;

    private readonly IStudentRepository
        _studentRepository;

    public PhysicalAccessOverrideService(
        IPhysicalAccessOverrideRepository overrideRepository,
        IStudentRepository studentRepository)
    {
        _overrideRepository = overrideRepository;
        _studentRepository = studentRepository;
    }

    public async Task<PhysicalAccessOverrideResponse?> SetAsync(
        Guid gymId,
        Guid actorUserId,
        Guid studentId,
        SetPhysicalAccessOverrideRequest request)
    {
        if (studentId == Guid.Empty ||
            actorUserId == Guid.Empty ||
            !Enum.IsDefined(request.Type))
        {
            return null;
        }

        var student =
            await _studentRepository
                .GetByIdAndGymIdAsync(
                    studentId,
                    gymId);

        if (student is null ||
            student.ArchivedAt is not null)
        {
            return null;
        }

        var reason =
            string.IsNullOrWhiteSpace(request.Reason)
                ? null
                : request.Reason.Trim();

        PersistenceTextPolicy.ValidateMaxLength(
            reason,
            PersistenceTextPolicy
                .PhysicalAccessOverrideReasonMaxLength,
            "O motivo");

        var existingOverride =
            await _overrideRepository
                .GetByStudentAsync(
                    studentId,
                    gymId);

        if (existingOverride is null)
        {
            var accessOverride =
                new PhysicalAccessOverride
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    StudentId = studentId,
                    Type = request.Type,
                    Reason = reason,
                    ActorUserId = actorUserId,
                    CreatedAt = DateTime.UtcNow,
                    Student = student
                };

            await _overrideRepository
                .AddAsync(accessOverride);

            return ToResponse(accessOverride);
        }

        existingOverride.Type = request.Type;
        existingOverride.Reason = reason;
        existingOverride.ActorUserId = actorUserId;
        existingOverride.UpdatedAt = DateTime.UtcNow;

        await _overrideRepository
            .UpdateAsync(existingOverride);

        return ToResponse(existingOverride);
    }

    public async Task<PhysicalAccessOverrideResponse?> GetAsync(
        Guid gymId,
        Guid studentId)
    {
        var accessOverride =
            await _overrideRepository
                .GetByStudentAsync(
                    studentId,
                    gymId);

        return accessOverride is null
            ? null
            : ToResponse(accessOverride);
    }

    public async Task<bool> RemoveAsync(
        Guid gymId,
        Guid studentId)
    {
        var accessOverride =
            await _overrideRepository
                .GetByStudentAsync(
                    studentId,
                    gymId);

        if (accessOverride is null)
        {
            return false;
        }

        await _overrideRepository
            .DeleteAsync(accessOverride);

        return true;
    }

    private static PhysicalAccessOverrideResponse ToResponse(
        PhysicalAccessOverride accessOverride)
    {
        return new PhysicalAccessOverrideResponse
        {
            Id = accessOverride.Id,
            StudentId = accessOverride.StudentId,
            Type = accessOverride.Type,
            Reason = accessOverride.Reason,
            ActorUserId = accessOverride.ActorUserId,
            CreatedAt = accessOverride.CreatedAt,
            UpdatedAt = accessOverride.UpdatedAt
        };
    }
}