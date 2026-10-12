using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class PhysicalAccessService
{
    private readonly IPhysicalAccessCredentialRepository
        _credentialRepository;

    private readonly IPhysicalAccessOverrideRepository
        _overrideRepository;

    private readonly IPhysicalAccessEventRepository
        _eventRepository;

    private readonly IEnrollmentRepository
        _enrollmentRepository;

    private readonly IChargeRepository
        _chargeRepository;

    private readonly IGymTimeZoneProvider
        _gymTimeZoneProvider;

    public PhysicalAccessService(
        IPhysicalAccessCredentialRepository credentialRepository,
        IPhysicalAccessOverrideRepository overrideRepository,
        IPhysicalAccessEventRepository eventRepository,
        IEnrollmentRepository enrollmentRepository,
        IChargeRepository chargeRepository,
        IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _credentialRepository = credentialRepository;
        _overrideRepository = overrideRepository;
        _eventRepository = eventRepository;
        _enrollmentRepository = enrollmentRepository;
        _chargeRepository = chargeRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
    }

    public async Task<PhysicalAccessDecisionResponse> DecideAsync(
        Guid gymId,
        PhysicalAccessDecisionRequest request)
    {
        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da requisição é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderKey))
        {
            throw new ArgumentException(
                "O identificador do provedor é obrigatório.");
        }

        if (!Enum.IsDefined(request.CredentialType))
        {
            throw new ArgumentException(
                "O tipo da credencial é inválido.");
        }

        if (string.IsNullOrWhiteSpace(request.ExternalIdentifier))
        {
            throw new ArgumentException(
                "O identificador externo é obrigatório.");
        }

        if (request.OccurredAt == default)
        {
            throw new ArgumentException(
                "A data da tentativa de acesso é obrigatória.");
        }

        var existingEvent =
            await _eventRepository.GetByRequestIdAsync(
                gymId,
                request.RequestId);

        if (existingEvent is not null)
        {
            return ToResponse(existingEvent);
        }

        var credential =
            await _credentialRepository
                .GetByExternalIdentifierAsync(
                    gymId,
                    request.ProviderKey.Trim(),
                    request.CredentialType,
                    request.ExternalIdentifier.Trim());

        if (credential is null)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                null,
                null,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.CredentialNotFound);
        }

        if (!credential.IsActive)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                credential.StudentId,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.CredentialInactive);
        }

        var student = credential.Student;

        if (student.ArchivedAt is not null)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.StudentArchived);
        }

        if (!student.User.IsActive)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.StudentInactive);
        }

        var accessOverride =
            await _overrideRepository.GetByStudentAsync(
                student.Id,
                gymId);

        if (accessOverride?.Type ==
            PhysicalAccessOverrideType.Block)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.ManualBlock);
        }

        if (accessOverride?.Type ==
            PhysicalAccessOverrideType.Allow)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Allowed,
                PhysicalAccessDecisionReason.ManualOverride);
        }

        var today = GetToday(gymId);

        var enrollment =
            await _enrollmentRepository
                .GetActiveByStudentAsync(
                    student.Id,
                    gymId,
                    today);

        if (enrollment is null)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.NoValidEnrollment);
        }

        var charge =
            await _chargeRepository
                .GetByEnrollmentIdAsync(
                    enrollment.Id,
                    gymId);

        if (charge is null ||
            charge.Status != ChargeStatus.Paid)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.FinancialRestriction);
        }

        return await RegisterDecisionAsync(
            gymId,
            request,
            student.Id,
            credential.Id,
            PhysicalAccessDecision.Allowed,
            PhysicalAccessDecisionReason.Eligible);
    }

    public async Task<PhysicalAccessDecisionResponse> SyncOfflineEventAsync(
    Guid gymId,
    SyncOfflinePhysicalAccessEventRequest request)
    {
        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da requisição é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderKey))
        {
            throw new ArgumentException(
                "O identificador do provedor é obrigatório.");
        }

        if (!Enum.IsDefined(request.CredentialType))
        {
            throw new ArgumentException(
                "O tipo da credencial é inválido.");
        }

        if (string.IsNullOrWhiteSpace(request.ExternalIdentifier))
        {
            throw new ArgumentException(
                "O identificador externo é obrigatório.");
        }

        if (!Enum.IsDefined(request.Decision))
        {
            throw new ArgumentException(
                "A decisão de acesso é inválida.");
        }

        if (!Enum.IsDefined(request.Reason))
        {
            throw new ArgumentException(
                "O motivo da decisão é inválido.");
        }

        if (!Enum.IsDefined(request.Source) ||
            request.Source == PhysicalAccessDecisionSource.Online)
        {
            throw new ArgumentException(
                "A origem da decisão offline é inválida.");
        }

        if (request.OccurredAt == default)
        {
            throw new ArgumentException(
                "A data da tentativa de acesso é obrigatória.");
        }

        if (request.ProcessedAt == default)
        {
            throw new ArgumentException(
                "A data de processamento da decisão é obrigatória.");
        }

        if (request.Source ==
                PhysicalAccessDecisionSource.OfflineFailClosed &&
            (request.Decision != PhysicalAccessDecision.Denied ||
             request.Reason !=
                 PhysicalAccessDecisionReason.OfflineNoCachedPermission))
        {
            throw new ArgumentException(
                "Uma decisão OfflineFailClosed deve ser negada por ausência de permissão em cache.");
        }

        var existingEvent =
            await _eventRepository.GetByRequestIdAsync(
                gymId,
                request.RequestId);

        if (existingEvent is not null)
        {
            return ToResponse(existingEvent);
        }

        var credential =
            await _credentialRepository
                .GetByExternalIdentifierAsync(
                    gymId,
                    request.ProviderKey.Trim(),
                    request.CredentialType,
                    request.ExternalIdentifier.Trim());

        var accessEvent =
            new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                RequestId = request.RequestId,
                StudentId = credential?.StudentId,
                CredentialId = credential?.Id,
                Decision = request.Decision,
                Reason = request.Reason,
                Source = request.Source,
                OccurredAt = request.OccurredAt,
                ProcessedAt = request.ProcessedAt
            };

        var persistedEvent =
            await _eventRepository.AddAsync(
                accessEvent);

        return ToResponse(persistedEvent);
    }

    public async Task<PagedResponse<PhysicalAccessEventHistoryItemResponse>>
        GetEventHistoryAsync(
            Guid gymId,
            Guid? studentId,
            PhysicalAccessDecision? decision,
            PhysicalAccessDecisionSource? source,
            DateOnly? fromDate,
            DateOnly? toDate,
            int page,
            int pageSize)
    {
        if (gymId == Guid.Empty)
            throw new ArgumentException("A academia é obrigatória.");

        if (studentId == Guid.Empty)
            throw new ArgumentException("O identificador do aluno é inválido.");

        if (decision.HasValue && !Enum.IsDefined(decision.Value))
            throw new ArgumentException("A decisão de acesso é inválida.");

        if (source.HasValue && !Enum.IsDefined(source.Value))
            throw new ArgumentException("A origem da decisão é inválida.");

        if (page < 1)
            throw new ArgumentException("A página deve ser maior ou igual a 1.");

        if (pageSize < 1 || pageSize > 100)
            throw new ArgumentException("O tamanho da página deve estar entre 1 e 100.");

        if (fromDate.HasValue && toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            throw new ArgumentException(
                "A data inicial não pode ser posterior à data final.");
        }

        var timeZone = _gymTimeZoneProvider.GetTimeZone(gymId);

        static DateTime ToUtcStart(DateOnly date, TimeZoneInfo zone)
        {
            var local = date.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }

        DateTime? occurredFrom = fromDate.HasValue
            ? ToUtcStart(fromDate.Value, timeZone)
            : null;

        DateTime? occurredTo = toDate.HasValue
            ? ToUtcStart(toDate.Value.AddDays(1), timeZone)
            : null;

        var skip = checked((page - 1) * pageSize);

        var result = await _eventRepository.GetPagedAsync(
            gymId,
            studentId,
            decision,
            source,
            occurredFrom,
            occurredTo,
            skip,
            pageSize);

        return new PagedResponse<PhysicalAccessEventHistoryItemResponse>
        {
            Items = result.Items.Select(accessEvent =>
                new PhysicalAccessEventHistoryItemResponse
                {
                    Id = accessEvent.Id,
                    RequestId = accessEvent.RequestId,
                    StudentId = accessEvent.StudentId,
                    StudentName = accessEvent.Student?.User?.Name,
                    Decision = accessEvent.Decision,
                    Reason = accessEvent.Reason,
                    Source = accessEvent.Source,
                    OccurredAt = accessEvent.OccurredAt,
                    ProcessedAt = accessEvent.ProcessedAt
                }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount
        };
    }
    private DateOnly GetToday(Guid gymId)
    {
        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var localNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone);

        return DateOnly.FromDateTime(localNow);
    }

    private async Task<PhysicalAccessDecisionResponse>
        RegisterDecisionAsync(
            Guid gymId,
            PhysicalAccessDecisionRequest request,
            Guid? studentId,
            Guid? credentialId,
            PhysicalAccessDecision decision,
            PhysicalAccessDecisionReason reason)
    {
        var accessEvent = new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            RequestId = request.RequestId,
            StudentId = studentId,
            CredentialId = credentialId,
            Decision = decision,
            Reason = reason,
            OccurredAt = request.OccurredAt,
            ProcessedAt = DateTime.UtcNow
        };

        var persistedEvent =
            await _eventRepository.AddAsync(
            accessEvent);

        return ToResponse(persistedEvent);
    }

    private static PhysicalAccessDecisionResponse ToResponse(
        PhysicalAccessEvent accessEvent)
    {
        return new PhysicalAccessDecisionResponse
        {
            RequestId = accessEvent.RequestId,
            Decision = accessEvent.Decision,
            Reason = accessEvent.Reason,
            StudentId = accessEvent.StudentId,
            CredentialId = accessEvent.CredentialId,
            ProcessedAt = accessEvent.ProcessedAt
        };
    }
}