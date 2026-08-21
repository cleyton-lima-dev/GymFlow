namespace GymFlow.Application.DTOs.PhysicalAssessments;

public class PhysicalAssessmentHistoryItemResponse
{
    public Guid Id { get; set; }

    public DateOnly AssessmentDate { get; set; }

    public decimal WeightKg { get; set; }

    public decimal HeightCm { get; set; }

    public decimal? BodyFatPercentage { get; set; }

    public DateOnly NextAssessmentDate { get; set; }

    public bool IsReassessmentDue { get; set; }
}