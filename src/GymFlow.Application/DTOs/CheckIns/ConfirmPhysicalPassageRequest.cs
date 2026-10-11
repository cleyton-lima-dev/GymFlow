namespace GymFlow.Application.DTOs.CheckIns;

public class ConfirmPhysicalPassageRequest
{
    public Guid RequestId { get; set; }

    public DateTime OccurredAt { get; set; }
}