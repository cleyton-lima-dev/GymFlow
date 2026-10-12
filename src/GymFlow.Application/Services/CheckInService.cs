using GymFlow.Application.DTOs.CheckIns;
using GymFlow.Application.DTOs.Common;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Services;

public class CheckInService
{
    private readonly ICheckInRepository
        _checkInRepository;

    private readonly IStudentRepository
        _studentRepository;

    private readonly IPhysicalAccessEventRepository
        _physicalAccessEventRepository;

    private readonly IGymTimeZoneProvider
        _gymTimeZoneProvider;

    public CheckInService(
        ICheckInRepository checkInRepository,
        IStudentRepository studentRepository,
        IPhysicalAccessEventRepository physicalAccessEventRepository,
        IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _checkInRepository =
            checkInRepository;

        _studentRepository =
            studentRepository;

        _physicalAccessEventRepository =
            physicalAccessEventRepository;

        _gymTimeZoneProvider =
            gymTimeZoneProvider;
    }

    public async Task<CheckInResponse?> CreateManualAsync(
        Guid gymId,
        Guid recordedByUserId,
        CreateManualCheckInRequest request)
    {
        if (gymId == Guid.Empty)
        {
            throw new ArgumentException(
                "A academia é obrigatória.");
        }

        if (recordedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "O usuário responsável é obrigatório.");
        }

        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da requisição é obrigatório.");
        }

        if (request.StudentId == Guid.Empty)
        {
            throw new ArgumentException(
                "O aluno é obrigatório.");
        }

        var existing =
            await _checkInRepository
                .GetByRequestIdAsync(
                    gymId,
                    request.RequestId);

        if (existing is not null)
        {
            if (existing.Source !=
                    CheckInSource.Manual ||
                existing.StudentId !=
                    request.StudentId)
            {
                throw new InvalidOperationException(
                    "O RequestId informado já foi utilizado por outro check-in.");
            }

            var existingStudent =
                await _studentRepository
                    .GetByIdAndGymIdAsync(
                        existing.StudentId,
                        gymId);

            if (existingStudent is null)
            {
                throw new InvalidOperationException(
                    "O check-in existente referencia um aluno inválido.");
            }

            return ToResponse(
                existing,
                existingStudent);
        }

        var student =
            await _studentRepository
                .GetByIdAndGymIdAsync(
                    request.StudentId,
                    gymId);

        if (student is null)
            return null;

        if (student.ArchivedAt is not null)
        {
            throw new InvalidOperationException(
                "Não é possível registrar check-in para um aluno arquivado.");
        }

        var now =
            DateTime.UtcNow;

        var checkIn =
            new CheckIn
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = student.Id,
                RequestId = request.RequestId,
                Source = CheckInSource.Manual,
                RecordedByUserId =
                    recordedByUserId,
                OccurredAt = now,
                CreatedAt = now
            };

        var persisted =
            await _checkInRepository
                .AddAsync(checkIn);

        return ToResponse(
            persisted,
            student);
    }

    public async Task<CheckInResponse?>
        CreateFromPhysicalPassageAsync(
            Guid gymId,
            ConfirmPhysicalPassageRequest request)
    {
        if (gymId == Guid.Empty)
        {
            throw new ArgumentException(
                "A academia é obrigatória.");
        }

        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da requisição é obrigatório.");
        }

        if (request.OccurredAt == default)
        {
            throw new ArgumentException(
                "A data da passagem é obrigatória.");
        }

        var existingCheckIn =
            await _checkInRepository
                .GetByRequestIdAsync(
                    gymId,
                    request.RequestId);

        if (existingCheckIn is not null)
        {
            if (existingCheckIn.Source !=
                CheckInSource.PhysicalAccess)
            {
                throw new InvalidOperationException(
                    "O RequestId informado já foi utilizado por outro check-in.");
            }

            var existingStudent =
                await _studentRepository
                    .GetByIdAndGymIdAsync(
                        existingCheckIn.StudentId,
                        gymId);

            if (existingStudent is null)
            {
                throw new InvalidOperationException(
                    "O check-in existente referencia um aluno inválido.");
            }

            return ToResponse(
                existingCheckIn,
                existingStudent);
        }

        var accessEvent =
            await _physicalAccessEventRepository
                .GetByRequestIdAsync(
                    gymId,
                    request.RequestId);

        if (accessEvent is null)
            return null;

        if (accessEvent.Decision !=
            PhysicalAccessDecision.Allowed)
        {
            throw new InvalidOperationException(
                "Não é possível registrar presença para um acesso negado.");
        }

        if (!accessEvent.StudentId.HasValue)
        {
            throw new InvalidOperationException(
                "O acesso autorizado não possui aluno associado.");
        }

        var student =
            await _studentRepository
                .GetByIdAndGymIdAsync(
                    accessEvent.StudentId.Value,
                    gymId);

        if (student is null)
        {
            throw new InvalidOperationException(
                "O evento de acesso referencia um aluno inválido.");
        }

        var checkIn =
            new CheckIn
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = student.Id,
                RequestId =
                    accessEvent.RequestId,
                Source =
                    CheckInSource.PhysicalAccess,
                PhysicalAccessEventId =
                    accessEvent.Id,
                RecordedByUserId = null,
                OccurredAt =
                    request.OccurredAt,
                CreatedAt =
                    DateTime.UtcNow
            };

        var persisted =
            await _checkInRepository
                .AddAsync(checkIn);

        return ToResponse(
            persisted,
            student);
    }

    public async Task<PagedResponse<CheckInHistoryItemResponse>>
        GetHistoryAsync(
            Guid gymId,
            Guid? studentId,
            DateOnly? fromDate,
            DateOnly? toDate,
            int page,
            int pageSize)
    {
        if (gymId == Guid.Empty)
        {
            throw new ArgumentException(
                "A academia é obrigatória.");
        }

        if (page < 1)
        {
            throw new ArgumentException(
                "A página deve ser maior ou igual a 1.");
        }

        if (pageSize < 1 ||
            pageSize > 100)
        {
            throw new ArgumentException(
                "O tamanho da página deve estar entre 1 e 100.");
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            throw new ArgumentException(
                "A data inicial não pode ser posterior à data final.");
        }

        var timeZone =
            _gymTimeZoneProvider
                .GetTimeZone(gymId);

        DateTime? occurredFrom =
            fromDate.HasValue
                ? ToUtcStart(
                    fromDate.Value,
                    timeZone)
                : null;

        DateTime? occurredTo =
            toDate.HasValue
                ? ToUtcStart(
                    toDate.Value.AddDays(1),
                    timeZone)
                : null;

        var skip =
            checked(
                (page - 1) *
                pageSize);

        var result =
            await _checkInRepository
                .GetPagedAsync(
                    gymId,
                    studentId,
                    occurredFrom,
                    occurredTo,
                    skip,
                    pageSize);

        return new PagedResponse<
            CheckInHistoryItemResponse>
        {
            Items =
                result.Items
                    .Select(checkIn =>
                        new CheckInHistoryItemResponse
                        {
                            Id =
                                checkIn.Id,
                            StudentId =
                                checkIn.StudentId,
                            StudentName =
                                checkIn.Student.User.Name,
                            Source =
                                checkIn.Source,
                            PhysicalAccessEventId =
                                checkIn.PhysicalAccessEventId,
                            RecordedByUserId =
                                checkIn.RecordedByUserId,
                            OccurredAt =
                                checkIn.OccurredAt
                        })
                    .ToList(),

            Page =
                page,

            PageSize =
                pageSize,

            TotalCount =
                result.TotalCount
        };
    }

    private static DateTime ToUtcStart(
        DateOnly date,
        TimeZoneInfo timeZone)
    {
        var local =
            date.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);

        return TimeZoneInfo
            .ConvertTimeToUtc(
                local,
                timeZone);
    }
    private static CheckInResponse ToResponse(
        CheckIn checkIn,
        Student student)
    {
        return new CheckInResponse
        {
            Id = checkIn.Id,
            StudentId = checkIn.StudentId,
            StudentName = student.User.Name,
            RequestId = checkIn.RequestId,
            Source = checkIn.Source,
            PhysicalAccessEventId =
                checkIn.PhysicalAccessEventId,
            RecordedByUserId =
                checkIn.RecordedByUserId,
            OccurredAt = checkIn.OccurredAt,
            CreatedAt = checkIn.CreatedAt
        };
    }
}