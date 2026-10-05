using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.PhysicalAccess;

public class PhysicalAccessOverrideResponse
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public PhysicalAccessOverrideType Type { get; set; }

    public string? Reason { get; set; }

    public Guid ActorUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}