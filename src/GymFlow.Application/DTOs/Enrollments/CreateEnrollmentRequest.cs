using GymFlow.Application.DTOs.Charges;

namespace GymFlow.Application.DTOs.Enrollments;

public class CreateEnrollmentRequest
{
    public Guid StudentId { get; set; }

    public Guid PlanId { get; set; }

    public DateOnly StartDate { get; set; }

    public PaymentDetailsRequest? Payment { get; set; }
}