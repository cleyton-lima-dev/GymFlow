using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.PhysicalAssessments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;

namespace GymFlow.Application.Services;

public class PhysicalAssessmentService
{
    private readonly IPhysicalAssessmentRepository _physicalAssessmentRepository;
    private readonly IStudentRepository _studentRepository;

    public PhysicalAssessmentService(
        IPhysicalAssessmentRepository physicalAssessmentRepository,
        IStudentRepository studentRepository)
    {
        _physicalAssessmentRepository = physicalAssessmentRepository;
        _studentRepository = studentRepository;
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

        ValidateRequest(request);

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

        await _physicalAssessmentRepository.AddAsync(assessment);
        await _physicalAssessmentRepository.SaveChangesAsync();

        return CreatePhysicalAssessmentResult.Success;
    }

    public async Task<PhysicalAssessmentResponse?> GetLatestAsync(
        Guid studentId,
        Guid gymId)
    {
        var assessment = await _physicalAssessmentRepository
            .GetLatestByStudentAsync(studentId, gymId);

        return assessment is null
            ? null
            : MapToResponse(assessment);
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
            ? null
            : MapToResponse(assessment);
    }

    public async Task<PagedResponse<PhysicalAssessmentHistoryItemResponse>>
        GetHistoryAsync(
            Guid studentId,
            Guid gymId,
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

        var assessments =
            await _physicalAssessmentRepository.GetHistoryByStudentAsync(
                studentId,
                gymId,
                page,
                pageSize);

        var totalCount =
            await _physicalAssessmentRepository.CountByStudentAsync(
                studentId,
                gymId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = assessments
            .Select(assessment =>
            {
                var nextAssessmentDate =
                    assessment.AssessmentDate.AddMonths(2);

                return new PhysicalAssessmentHistoryItemResponse
                {
                    Id = assessment.Id,
                    AssessmentDate = assessment.AssessmentDate,
                    WeightKg = assessment.WeightKg,
                    HeightCm = assessment.HeightCm,
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
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            throw new KeyNotFoundException("Aluno não encontrado.");

        return await GetHistoryAsync(
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

    private static PhysicalAssessmentResponse MapToResponse(
        PhysicalAssessment assessment)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

    private static void ValidateRequest(
        CreatePhysicalAssessmentRequest request)
    {
        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.AssessmentDate > today)
        {
            throw new ArgumentException(
                "A data da avaliação não pode estar no futuro.");
        }

        if (request.WeightKg <= 0)
        {
            throw new ArgumentException(
                "O peso deve ser maior que zero.");
        }

        if (request.HeightCm <= 0)
        {
            throw new ArgumentException(
                "A altura deve ser maior que zero.");
        }

        if (request.BodyFatPercentage.HasValue &&
            (request.BodyFatPercentage.Value < 0 ||
             request.BodyFatPercentage.Value > 100))
        {
            throw new ArgumentException(
                "O percentual de gordura deve estar entre 0 e 100.");
        }

        ValidatePositive(request.ChestCm, "Peito");
        ValidatePositive(request.WaistCm, "Cintura");
        ValidatePositive(request.AbdomenCm, "Abdômen");
        ValidatePositive(request.HipCm, "Quadril");

        ValidatePositive(request.RightArmCm, "Braço direito");
        ValidatePositive(request.LeftArmCm, "Braço esquerdo");

        ValidatePositive(request.RightThighCm, "Coxa direita");
        ValidatePositive(request.LeftThighCm, "Coxa esquerda");

        ValidatePositive(request.RightCalfCm, "Panturrilha direita");
        ValidatePositive(request.LeftCalfCm, "Panturrilha esquerda");

        if (request.Notes?.Length > 500)
        {
            throw new ArgumentException(
                "As observações devem possuir no máximo 500 caracteres.");
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