using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class SetPhysicalAccessOverrideRequest
{
    public PhysicalAccessOverrideType Type { get; set; }

    public string? Reason { get; set; }
}