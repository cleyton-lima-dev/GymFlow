using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.PhysicalAssessments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Exceptions;

namespace GymFlow.Application.Services;

public class PhysicalAssessmentService
{
    private readonly IPhysicalAssessmentRepository _physicalAssessmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private const decimal MaxDatabaseDecimalValue = 999.99m;

    public PhysicalAssessmentService(
    IPhysicalAssessmentRepository physicalAssessmentRepository,
    IStudentRepository studentRepository,
    IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _physicalAssessmentRepository = physicalAssessmentRepository;
        _studentRepository = studentRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
    }

    public async Task<CreatePhysicalAssessmentResult> CreateAsync(
        Guid studentId,
        Guid gymId,
        CreatePhysicalAssessmentRequest request)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            return CreatePhysicalAssessmentResult.StudentNotFound;

        ValidateRequest(request, gymId);

        var existsForDate =
            await _physicalAssessmentRepository.ExistsForDateAsync(
                studentId,
                gymId,
                request.AssessmentDate);

        if (existsForDate)
        {
            return CreatePhysicalAssessmentResult
                .AssessmentAlreadyExistsForDate;
        }

        var now = DateTime.UtcNow;

        var assessment = new PhysicalAssessment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            AssessmentDate = request.AssessmentDate,

            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,

            BodyFatPercentage = request.BodyFatPercentage,

            ChestCm = request.ChestCm,
            WaistCm = request.WaistCm,
            AbdomenCm = request.AbdomenCm,
            HipCm = request.HipCm,

            RightArmCm = request.RightArmCm,
            LeftArmCm = request.LeftArmCm,

            RightThighCm = request.RightThighCm,
            LeftThighCm = request.LeftThighCm,

            RightCalfCm = request.RightCalfCm,
            LeftCalfCm = request.LeftCalfCm,

            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim(),

            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _physicalAssessmentRepository.AddAsync(assessment);
            await _physicalAssessmentRepository.SaveChangesAsync();
        }
        catch (PhysicalAssessmentDateConflictException)
        {
            return CreatePhysicalAssessmentResult
                .AssessmentAlreadyExistsForDate;
        }

        return CreatePhysicalAssessmentResult.Success;
    }

    public async Task<bool> UpdateAsync(
    Guid assessmentId,
    Guid studentId,
    Guid gymId,
    UpdatePhysicalAssessmentRequest request)
    {
        var assessment = await _physicalAssessmentRepository
            .GetByIdForUpdateAsync(
                assessmentId,
                studentId,
                gymId);

        if (assessment is null)
            return false;

        ValidateUpdateRequest(request);

        assessment.WeightKg = request.WeightKg;
        assessment.HeightCm = request.HeightCm;

        assessment.BodyFatPercentage = request.BodyFatPercentage;

        assessment.ChestCm = request.ChestCm;
        assessment.WaistCm = request.WaistCm;
        assessment.AbdomenCm = request.AbdomenCm;
        assessment.HipCm = request.HipCm;

        assessment.RightArmCm = request.RightArmCm;
        assessment.LeftArmCm = request.LeftArmCm;

        assessment.RightThighCm = request.RightThighCm;
        assessment.LeftThighCm = request.LeftThighCm;

        assessment.RightCalfCm = request.RightCalfCm;
        assessment.LeftCalfCm = request.LeftCalfCm;

        assessment.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        assessment.UpdatedAt = DateTime.UtcNow;

        await _physicalAssessmentRepository.SaveChangesAsync();

        return true;
    }

    public async Task<PhysicalAssessmentResponse?> GetLatestAsync(
        Guid studentId,
        Guid gymId)
    {
        var assessment = await _physicalAssessmentRepository
            .GetLatestByStudentAsync(studentId, gymId);

        return assessment is null
            ? null
            : MapToResponse(assessment, gymId);
    }

    public async Task<PhysicalAssessmentResponse?> GetByIdAsync(
        Guid assessmentId,
        Guid studentId,
        Guid gymId)
    {
        var assessment = await _physicalAssessmentRepository
            .GetByIdAsync(
                assessmentId,
                studentId,
                gymId);

        return assessment is null
            ? null:
            MapToResponse(assessment, gymId);
    }

    public async Task<PagedResponse<PhysicalAssessmentHistoryItemResponse>>
    GetHistoryAsync(
        Guid studentId,
        Guid gymId,
        int page,
        int pageSize)
    {
        ValidatePagination(
            page,
            pageSize);

        var student = await _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId);

        if (student is null)
        {
            throw new KeyNotFoundException(
                "Aluno não encontrado.");
        }

        return await GetHistoryCoreAsync(
            student.Id,
            gymId,
            page,
            pageSize);
    }

    public async Task<PhysicalAssessmentResponse?> GetLatestForUserAsync(
    Guid userId,
    Guid gymId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            return null;

        return await GetLatestAsync(student.Id, gymId);
    }

    public async Task<PagedResponse<PhysicalAssessmentHistoryItemResponse>>
    GetHistoryForUserAsync(
        Guid userId,
        Guid gymId,
        int page,
        int pageSize)
    {
        ValidatePagination(
            page,
            pageSize);

        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        if (student is null)
        {
            throw new KeyNotFoundException(
                "Aluno não encontrado.");
        }

        return await GetHistoryCoreAsync(
            student.Id,
            gymId,
            page,
            pageSize);
    }

    public async Task<PhysicalAssessmentResponse?> GetByIdForUserAsync(
        Guid assessmentId,
        Guid userId,
        Guid gymId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            return null;

        return await GetByIdAsync(
            assessmentId,
            student.Id,
            gymId);
    }

    private static void ValidatePagination(
    int page,
    int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentException(
                "A página deve ser maior ou igual a 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentException(
                "O tamanho da página deve estar entre 1 e 100.");
        }
    }

    private async Task<PagedResponse<PhysicalAssessmentHistoryItemResponse>>
        GetHistoryCoreAsync(
            Guid studentId,
            Guid gymId,
            int page,
            int pageSize)
    {
        var assessments =
            await _physicalAssessmentRepository
                .GetHistoryByStudentAsync(
                    studentId,
                    gymId,
                    page,
                    pageSize);

        var totalCount =
            await _physicalAssessmentRepository
                .CountByStudentAsync(
                    studentId,
                    gymId);

        var today = GetGymDate(
            gymId,
            DateTime.UtcNow);

        var items = assessments
            .Select(assessment =>
            {
                var nextAssessmentDate =
                    assessment.AssessmentDate.AddMonths(2);

                return new PhysicalAssessmentHistoryItemResponse
                {
                    Id = assessment.Id,
                    AssessmentDate =
                        assessment.AssessmentDate,

                    WeightKg =
                        assessment.WeightKg,

                    HeightCm =
                        assessment.HeightCm,

                    BodyFatPercentage =
                        assessment.BodyFatPercentage,

                    NextAssessmentDate =
                        nextAssessmentDate,

                    IsReassessmentDue =
                        today >= nextAssessmentDate
                };
            })
            .ToList();

        return new PagedResponse<PhysicalAssessmentHistoryItemResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public DateOnly GetCurrentGymDate(Guid gymId)
    {
        return GetGymDate(
            gymId,
            DateTime.UtcNow);
    }

    private PhysicalAssessmentResponse MapToResponse(
    PhysicalAssessment assessment,
    Guid gymId)
    {
        var today = GetGymDate(
            gymId,
            DateTime.UtcNow);

        var nextAssessmentDate =
            assessment.AssessmentDate.AddMonths(2);

        return new PhysicalAssessmentResponse
        {
            Id = assessment.Id,
            StudentId = assessment.StudentId,

            AssessmentDate = assessment.AssessmentDate,
            NextAssessmentDate = nextAssessmentDate,
            IsReassessmentDue =
                today >= nextAssessmentDate,

            WeightKg = assessment.WeightKg,
            HeightCm = assessment.HeightCm,
            BodyFatPercentage =
                assessment.BodyFatPercentage,

            ChestCm = assessment.ChestCm,
            WaistCm = assessment.WaistCm,
            AbdomenCm = assessment.AbdomenCm,
            HipCm = assessment.HipCm,

            RightArmCm = assessment.RightArmCm,
            LeftArmCm = assessment.LeftArmCm,

            RightThighCm = assessment.RightThighCm,
            LeftThighCm = assessment.LeftThighCm,

            RightCalfCm = assessment.RightCalfCm,
            LeftCalfCm = assessment.LeftCalfCm,

            Notes = assessment.Notes,
            CreatedAt = assessment.CreatedAt
        };
    }

    private DateOnly GetGymDate(
    Guid gymId,
    DateTime utcDateTime)
    {
        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var localDateTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                utcDateTime,
                timeZone);

        return DateOnly.FromDateTime(
            localDateTime);
    }

    private void ValidateRequest(
    CreatePhysicalAssessmentRequest request,
    Guid gymId)
    {
        var today = GetGymDate(
            gymId,
            DateTime.UtcNow);

        if (request.AssessmentDate == default)
        {
            throw new ArgumentException(
                "A data da avaliação é obrigatória.");
        }

        if (request.AssessmentDate > today)
        {
            throw new ArgumentException(
                "A data da avaliação não pode estar no futuro.");
        }

        ValidateRequiredMeasurement(
            request.WeightKg,
             "Peso");

        ValidateRequiredMeasurement(
            request.HeightCm,
            "Altura");

        if (request.BodyFatPercentage.HasValue &&
            (request.BodyFatPercentage.Value < 0 ||
             request.BodyFatPercentage.Value > 100))
        {
            throw new ArgumentException(
                "O percentual de gordura deve estar entre 0 e 100.");
        }

        ValidateOptionalMeasurement(
    request.ChestCm,
    "Peitoral");

        ValidateOptionalMeasurement(
            request.WaistCm,
            "Cintura");

        ValidateOptionalMeasurement(
            request.AbdomenCm,
            "Abdômen");

        ValidateOptionalMeasurement(
            request.HipCm,
            "Quadril");

        ValidateOptionalMeasurement(
            request.RightArmCm,
            "Braço direito");

        ValidateOptionalMeasurement(
            request.LeftArmCm,
            "Braço esquerdo");

        ValidateOptionalMeasurement(
            request.RightThighCm,
            "Coxa direita");

        ValidateOptionalMeasurement(
            request.LeftThighCm,
            "Coxa esquerda");

        ValidateOptionalMeasurement(
            request.RightCalfCm,
            "Panturrilha direita");

        ValidateOptionalMeasurement(
            request.LeftCalfCm,
            "Panturrilha esquerda");

        if (request.Notes?.Length > 500)
        {
            throw new ArgumentException(
                "As observações devem possuir no máximo 500 caracteres.");
        }
    }

    private void ValidateUpdateRequest(
    UpdatePhysicalAssessmentRequest request)
    {
        ValidateRequiredMeasurement(
            request.WeightKg,
            "Peso");

        ValidateRequiredMeasurement(
            request.HeightCm,
            "Altura");

        if (request.BodyFatPercentage.HasValue &&
            (request.BodyFatPercentage.Value < 0 ||
             request.BodyFatPercentage.Value > 100))
        {
            throw new ArgumentException(
                "O percentual de gordura deve estar entre 0 e 100.");
        }

        ValidateOptionalMeasurement(
            request.ChestCm,
            "Peitoral");

        ValidateOptionalMeasurement(
            request.WaistCm,
            "Cintura");

        ValidateOptionalMeasurement(
            request.AbdomenCm,
            "Abdômen");

        ValidateOptionalMeasurement(
            request.HipCm,
            "Quadril");

        ValidateOptionalMeasurement(
            request.RightArmCm,
            "Braço direito");

        ValidateOptionalMeasurement(
            request.LeftArmCm,
            "Braço esquerdo");

        ValidateOptionalMeasurement(
            request.RightThighCm,
            "Coxa direita");

        ValidateOptionalMeasurement(
            request.LeftThighCm,
            "Coxa esquerda");

        ValidateOptionalMeasurement(
            request.RightCalfCm,
            "Panturrilha direita");

        ValidateOptionalMeasurement(
            request.LeftCalfCm,
            "Panturrilha esquerda");

        if (request.Notes?.Length > 500)
        {
            throw new ArgumentException(
                "As observações devem possuir no máximo 500 caracteres.");
        }
    }
    private static void ValidateRequiredMeasurement(
    decimal value,
    string fieldName)
    {
        if (value <= 0)
        {
            throw new ArgumentException(
                $"{fieldName} deve ser maior que zero.");
        }

        if (value > MaxDatabaseDecimalValue)
        {
            throw new ArgumentException(
                $"{fieldName} excede o valor máximo permitido.");
        }
    }

    private static void ValidateOptionalMeasurement(
        decimal? value,
        string fieldName)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value <= 0)
        {
            throw new ArgumentException(
                $"{fieldName} deve ser maior que zero.");
        }

        if (value.Value > MaxDatabaseDecimalValue)
        {
            throw new ArgumentException(
                $"{fieldName} excede o valor máximo permitido.");
        }
    }

    private static void ValidatePositive(
        decimal? value,
        string fieldName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new ArgumentException(
                $"{fieldName} deve ser maior que zero.");
        }
    }
}