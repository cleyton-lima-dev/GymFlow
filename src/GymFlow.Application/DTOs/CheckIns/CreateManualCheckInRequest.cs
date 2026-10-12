namespace GymFlow.Application.DTOs.CheckIns;

public class CreateManualCheckInRequest
{
    public Guid RequestId { get; set; }

    public Guid StudentId { get; set; }
}