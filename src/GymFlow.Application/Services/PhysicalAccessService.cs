using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace GymFlow.Application.Services;

public class PhysicalAccessService
{
    private readonly IPhysicalAccessCredentialRepository
        _credentialRepository;

    private readonly IAccessAgentRepository _accessAgentRepository;

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

    private readonly IGymErpPolicy _erpPolicy;

    private readonly IPhysicalAccessPinProtector _pinProtector;
    private readonly IPhysicalAccessPinAttemptLimiter _pinAttemptLimiter;

    public PhysicalAccessService(
        IAccessAgentRepository accessAgentRepository,
        IPhysicalAccessCredentialRepository credentialRepository,
        IPhysicalAccessOverrideRepository overrideRepository,
        IPhysicalAccessEventRepository eventRepository,
        IEnrollmentRepository enrollmentRepository,
        IChargeRepository chargeRepository,
        IGymTimeZoneProvider gymTimeZoneProvider,
        IGymErpPolicy erpPolicy,
        IPhysicalAccessPinProtector pinProtector,
        IPhysicalAccessPinAttemptLimiter pinAttemptLimiter)
    {
        _accessAgentRepository = accessAgentRepository;
        _credentialRepository = credentialRepository;
        _overrideRepository = overrideRepository;
        _eventRepository = eventRepository;
        _enrollmentRepository = enrollmentRepository;
        _chargeRepository = chargeRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
        _erpPolicy = erpPolicy;
        _pinProtector = pinProtector;
        _pinAttemptLimiter = pinAttemptLimiter;
    }

    public Task<PhysicalAccessDecisionResponse> DecideAsync(
        Guid gymId,
        PhysicalAccessDecisionRequest request) =>
        DecideCoreAsync(gymId, null, null, request);

    public Task<PhysicalAccessDecisionResponse> DecideAsync(
        Guid gymId,
        Guid accessAgentId,
        PhysicalAccessDecisionRequest request) =>
        DecideCoreAsync(gymId, accessAgentId, request.DeviceKey, request);

    private async Task<PhysicalAccessDecisionResponse> DecideCoreAsync(
        Guid gymId,
        Guid? accessAgentId,
        string? deviceKey,
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

        if (accessAgentId.HasValue)
            deviceKey = await ResolveConfiguredDeviceKeyAsync(
                gymId, accessAgentId.Value, deviceKey, request.ProviderKey,
                requireEnabled: true);

        var requestFingerprint = CreateRequestFingerprint(
            gymId, request.RequestId, accessAgentId,
            accessAgentId.HasValue ? deviceKey : request.DeviceKey,
            request.ProviderKey, request.CredentialType,
            request.ExternalIdentifier, request.OccurredAt,
            PhysicalAccessDecisionSource.Online, null, null, null);

        var existingEvent =
            await _eventRepository.GetByRequestIdAsync(
                gymId,
                request.RequestId);

        if (existingEvent is not null)
        {
            EnsureEventIdentity(existingEvent, gymId, request.RequestId);
            EnsureSameOrigin(existingEvent, accessAgentId, deviceKey);
            EnsureSameFingerprint(existingEvent, requestFingerprint);
            return ToResponse(existingEvent);
        }

        var identifier = request.ExternalIdentifier.Trim();
        PhysicalAccessPinReservation? reservation = null;
        var pinFailed = false;
        if (request.CredentialType == PhysicalAccessCredentialType.PinExternalId)
        {
            if (identifier.Length != 4 ||
                !identifier.All(character => character is >= '0' and <= '9'))
            {
                throw new ArgumentException("O PIN deve conter exatamente quatro dígitos.");
            }

            reservation = await _pinAttemptLimiter.TryReserveAsync(gymId);
            if (reservation is null)
            {
                return await RegisterDecisionAsync(gymId, request, null, null,
                    PhysicalAccessDecision.Denied,
                    PhysicalAccessDecisionReason.PinRateLimited,
                    accessAgentId, deviceKey, requestFingerprint);
            }

        }

        try
        {
        if (reservation is not null)
            identifier = _pinProtector.ComputeLookupHash(gymId, identifier);

        var credential =
            await _credentialRepository
                .GetByExternalIdentifierAsync(
                    gymId,
                    request.ProviderKey.Trim(),
                    request.CredentialType,
                    identifier);

        if (credential is null)
        {
            pinFailed = reservation is not null;

            return await RegisterDecisionAsync(
                gymId,
                request,
                null,
                null,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.CredentialNotFound,
                accessAgentId, deviceKey, requestFingerprint);
        }

        if (!credential.IsActive)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                credential.StudentId,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.CredentialInactive,
                accessAgentId, deviceKey, requestFingerprint);
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
                PhysicalAccessDecisionReason.StudentArchived,
                accessAgentId, deviceKey, requestFingerprint);
        }

        if (!student.IsActive)
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionReason.StudentInactive,
                accessAgentId, deviceKey, requestFingerprint);
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
                PhysicalAccessDecisionReason.ManualBlock,
                accessAgentId, deviceKey, requestFingerprint);
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
                PhysicalAccessDecisionReason.ManualOverride,
                accessAgentId, deviceKey, requestFingerprint);
        }

        // Shared physical access does not require ERP enrollment or payment.
        // Resolve the authenticated tenant policy before granting this exception.
        if (!_erpPolicy.IsEnabled(gymId))
        {
            return await RegisterDecisionAsync(
                gymId,
                request,
                student.Id,
                credential.Id,
                PhysicalAccessDecision.Allowed,
                PhysicalAccessDecisionReason.Eligible,
                accessAgentId, deviceKey, requestFingerprint);
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
                PhysicalAccessDecisionReason.NoValidEnrollment,
                accessAgentId, deviceKey, requestFingerprint);
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
                PhysicalAccessDecisionReason.FinancialRestriction,
                accessAgentId, deviceKey, requestFingerprint);
        }

        return await RegisterDecisionAsync(
            gymId,
            request,
            student.Id,
            credential.Id,
            PhysicalAccessDecision.Allowed,
            PhysicalAccessDecisionReason.Eligible,
            accessAgentId, deviceKey, requestFingerprint);
        }
        finally
        {
            if (reservation is not null)
                await _pinAttemptLimiter.CompleteAsync(reservation, pinFailed);
        }
    }

    public async Task<PhysicalAccessDecisionResponse> SyncOfflineEventAsync(
    Guid gymId,
    Guid accessAgentId,
    SyncOfflinePhysicalAccessEventRequest request)
    {
        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da requisição é obrigatório.");
        }

        if (accessAgentId == Guid.Empty)
            throw new PhysicalAccessOriginConflictException();

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

        var isGrantReason = request.Reason is
            PhysicalAccessDecisionReason.Eligible or
            PhysicalAccessDecisionReason.ManualOverride;
        if (request.Source == PhysicalAccessDecisionSource.OfflineCache &&
            (request.Reason == PhysicalAccessDecisionReason.OfflineNoCachedPermission ||
             (request.Decision == PhysicalAccessDecision.Allowed) != isGrantReason))
        {
            throw new ArgumentException(
                "A decisão e o motivo são inconsistentes para uma decisão OfflineCache.");
        }

        var deviceKey = await ResolveConfiguredDeviceKeyAsync(
            gymId, accessAgentId, request.DeviceKey, request.ProviderKey,
            requireEnabled: false);
        var requestFingerprint = CreateRequestFingerprint(
            gymId, request.RequestId, accessAgentId, deviceKey,
            request.ProviderKey, request.CredentialType,
            request.ExternalIdentifier, request.OccurredAt,
            request.Source, request.Decision, request.Reason,
            request.ProcessedAt);

        var existingEvent =
            await _eventRepository.GetByRequestIdAsync(
                gymId,
                request.RequestId);

        if (existingEvent is not null)
        {
            EnsureEventIdentity(existingEvent, gymId, request.RequestId);
            EnsureSameOrigin(existingEvent, accessAgentId, deviceKey);
            EnsureSameFingerprint(existingEvent, requestFingerprint);
            return ToResponse(existingEvent);
        }

        var offlineIdentifier = request.ExternalIdentifier.Trim();
        var validPin = offlineIdentifier.Length == 4 &&
            offlineIdentifier.All(character => character is >= '0' and <= '9');
        var credential = request.CredentialType == PhysicalAccessCredentialType.PinExternalId && !validPin
            ? null
            : await _credentialRepository.GetByExternalIdentifierAsync(
                gymId, request.ProviderKey.Trim(), request.CredentialType,
                request.CredentialType == PhysicalAccessCredentialType.PinExternalId
                    ? _pinProtector.ComputeLookupHash(gymId, offlineIdentifier)
                    : offlineIdentifier);

        var accessEvent =
            new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                RequestId = request.RequestId,
                AccessAgentId = accessAgentId,
                DeviceKey = deviceKey,
                RequestFingerprint = requestFingerprint,
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

        EnsureEventIdentity(persistedEvent, gymId, request.RequestId);
        EnsureSameOrigin(persistedEvent, accessAgentId, deviceKey);
        EnsureSameFingerprint(persistedEvent, requestFingerprint);

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
                    StudentName = accessEvent.Student?.Name,
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
            PhysicalAccessDecisionReason reason,
            Guid? accessAgentId,
            string? deviceKey,
            string requestFingerprint)
    {
        var accessEvent = new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            RequestId = request.RequestId,
            AccessAgentId = accessAgentId,
            DeviceKey = deviceKey,
            RequestFingerprint = requestFingerprint,
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

        EnsureEventIdentity(persistedEvent, gymId, request.RequestId);
        EnsureSameOrigin(persistedEvent, accessAgentId, deviceKey);
        EnsureSameFingerprint(persistedEvent, requestFingerprint);

        return ToResponse(persistedEvent);
    }

    private async Task<string> ResolveConfiguredDeviceKeyAsync(
        Guid gymId,
        Guid accessAgentId,
        string? requestedDeviceKey,
        string providerKey,
        bool requireEnabled)
    {
        if (gymId == Guid.Empty || accessAgentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(requestedDeviceKey))
            throw new PhysicalAccessOriginConflictException();

        var agent = await _accessAgentRepository.GetByIdAsync(accessAgentId, gymId);
        if (agent is null || !agent.IsActive)
            throw new PhysicalAccessOriginConflictException();

        List<AccessAgentDeviceConfigurationDto> configuredDevices;
        try
        {
            configuredDevices = JsonSerializer.Deserialize<
                List<AccessAgentDeviceConfigurationDto>>(
                agent.DeviceConfigurationJson ?? "[]",
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];
        }
        catch (JsonException)
        {
            throw new PhysicalAccessOriginConflictException();
        }

        var configuredDevice = configuredDevices.FirstOrDefault(device =>
            string.Equals(device.DeviceKey?.Trim(), requestedDeviceKey.Trim(),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(device.ProviderKey?.Trim(), providerKey.Trim(),
                StringComparison.OrdinalIgnoreCase) &&
            (!requireEnabled || device.Enabled));

        return configuredDevice?.DeviceKey.Trim()
            ?? throw new PhysicalAccessOriginConflictException();
    }

    private static void EnsureSameOrigin(
        PhysicalAccessEvent accessEvent,
        Guid? accessAgentId,
        string? deviceKey)
    {
        var sameDevice = accessEvent.DeviceKey is null
            ? deviceKey is null
            : string.Equals(accessEvent.DeviceKey, deviceKey?.Trim(),
                StringComparison.OrdinalIgnoreCase);

        if (accessEvent.AccessAgentId != accessAgentId || !sameDevice)
            throw new PhysicalAccessOriginConflictException();
    }

    private static void EnsureEventIdentity(
        PhysicalAccessEvent accessEvent,
        Guid gymId,
        Guid requestId)
    {
        if (accessEvent.GymId != gymId || accessEvent.RequestId != requestId)
            throw new PhysicalAccessOriginConflictException();
    }

    private static void EnsureSameFingerprint(
        PhysicalAccessEvent accessEvent,
        string requestFingerprint)
    {
        if (accessEvent.RequestFingerprint is null)
            throw new PhysicalAccessOriginConflictException();

        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(accessEvent.RequestFingerprint),
                    Convert.FromHexString(requestFingerprint)))
                throw new PhysicalAccessOriginConflictException();
        }
        catch (FormatException)
        {
            throw new PhysicalAccessOriginConflictException();
        }
    }

    private string CreateRequestFingerprint(
        Guid gymId,
        Guid requestId,
        Guid? accessAgentId,
        string? deviceKey,
        string providerKey,
        PhysicalAccessCredentialType credentialType,
        string externalIdentifier,
        DateTime occurredAt,
        PhysicalAccessDecisionSource source,
        PhysicalAccessDecision? decision,
        PhysicalAccessDecisionReason? reason,
        DateTime? processedAt)
    {
        var normalizedIdentifier = externalIdentifier.Trim();
        var credentialDigest = credentialType == PhysicalAccessCredentialType.PinExternalId
            ? _pinProtector.ComputeLookupHash(gymId, normalizedIdentifier)
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedIdentifier)));
        var canonicalPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            GymId = gymId,
            RequestId = requestId,
            AccessAgentId = accessAgentId,
            DeviceKey = deviceKey?.Trim().ToUpperInvariant(),
            ProviderKey = providerKey.Trim(),
            CredentialType = (int)credentialType,
            CredentialDigest = credentialDigest,
            OccurredAtTicks = occurredAt.ToUniversalTime().Ticks,
            Source = (int)source,
            Decision = decision.HasValue ? (int?)decision.Value : null,
            Reason = reason.HasValue ? (int?)reason.Value : null,
            ProcessedAtTicks = processedAt?.ToUniversalTime().Ticks
        });
        return Convert.ToHexString(SHA256.HashData(canonicalPayload));
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
