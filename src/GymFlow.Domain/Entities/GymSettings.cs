namespace GymFlow.Domain.Entities;

public sealed class GymSettings
{
    public Guid GymId { get; set; }
    public string AdministrativeName { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
