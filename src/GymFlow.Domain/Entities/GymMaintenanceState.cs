namespace GymFlow.Domain.Entities;

public sealed class GymMaintenanceState
{
    public Guid GymId { get; set; }
    public bool IsActive { get; set; }
    public Guid? AllowedAdminUserId { get; set; }
    public long Version { get; set; }
    public DateTime UpdatedAt { get; set; }
}
