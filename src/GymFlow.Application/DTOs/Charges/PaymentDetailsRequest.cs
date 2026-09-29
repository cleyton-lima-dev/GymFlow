using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Charges;

public class PaymentDetailsRequest
{
    public decimal PaidAmount { get; set; }

    public decimal DiscountAmount { get; set; } = 0m;

    public PaymentMethod PaymentMethod { get; set; }

    public DateTimeOffset PaidAt { get; set; }
}