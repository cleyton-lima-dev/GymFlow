namespace GymFlow.Application.DTOs.PhysicalAssessments;

public class CreatePhysicalAssessmentRequest
{
    public DateOnly AssessmentDate { get; set; }

    public decimal WeightKg { get; set; }

    public decimal HeightCm { get; set; }

    public decimal? BodyFatPercentage { get; set; }

    public decimal? ChestCm { get; set; }

    public decimal? WaistCm { get; set; }

    public decimal? AbdomenCm { get; set; }

    public decimal? HipCm { get; set; }

    public decimal? RightArmCm { get; set; }

    public decimal? LeftArmCm { get; set; }

    public decimal? RightThighCm { get; set; }

    public decimal? LeftThighCm { get; set; }

    public decimal? RightCalfCm { get; set; }

    public decimal? LeftCalfCm { get; set; }

    public string? Notes { get; set; }
}