using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Students;

public class StudentFinancialHistoryItemResponse
{
    public Guid EnrollmentId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public decimal PlanPrice { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public EnrollmentStatus EnrollmentStatus { get; set; }

    public DateOnly? CancellationDate { get; set; }

    public Guid? ChargeId { get; set; }

    public decimal? ChargeAmount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? PaidAmount { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    public DateOnly? DueDate { get; set; }

    public ChargeStatus? ChargeStatus { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }
}