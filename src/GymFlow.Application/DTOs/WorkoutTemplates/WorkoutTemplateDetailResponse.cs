namespace GymFlow.Application.DTOs.WorkoutTemplates;

public class WorkoutTemplateDetailResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<WorkoutTemplateDayResponse> Days { get; set; } = new();
}