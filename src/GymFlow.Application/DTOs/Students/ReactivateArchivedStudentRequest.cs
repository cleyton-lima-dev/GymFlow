namespace GymFlow.Application.DTOs.Students;

public class ReactivateArchivedStudentRequest
{
    public Guid PlanId { get; set; }

    public DateOnly StartDate { get; set; }
}