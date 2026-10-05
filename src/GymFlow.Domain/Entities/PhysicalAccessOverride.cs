using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class PhysicalAccessOverride
{
    public Guid Id { get; set; }

    public Guid GymId { get; set; }

    public Guid StudentId { get; set; }

    public PhysicalAccessOverrideType Type { get; set; }

    public string? Reason { get; set; }

    public Guid ActorUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;

    public User ActorUser { get; set; } = null!;
}