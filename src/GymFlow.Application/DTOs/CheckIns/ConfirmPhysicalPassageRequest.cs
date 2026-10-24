namespace GymFlow.Application.DTOs.CheckIns;

public class ConfirmPhysicalPassageRequest
{
    public Guid RequestId { get; set; }

    public string DeviceKey { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
