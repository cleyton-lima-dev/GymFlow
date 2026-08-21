using GymFlow.Domain.Entities;

namespace GymFlow.Application.Interfaces.Repositories;

public interface IPhysicalAssessmentRepository
{
    Task AddAsync(PhysicalAssessment assessment);

    Task<PhysicalAssessment?> GetByIdAsync(
        Guid assessmentId,
        Guid studentId,
        Guid gymId);

    Task<PhysicalAssessment?> GetLatestByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task<List<PhysicalAssessment>> GetHistoryByStudentAsync(
        Guid studentId,
        Guid gymId,
        int page,
        int pageSize);

    Task<int> CountByStudentAsync(
        Guid studentId,
        Guid gymId);

    Task<bool> ExistsForDateAsync(
        Guid studentId,
        Guid gymId,
        DateOnly assessmentDate);

    Task SaveChangesAsync();
}