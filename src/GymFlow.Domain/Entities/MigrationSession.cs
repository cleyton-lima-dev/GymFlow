using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public sealed class MigrationSession
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceNamespace { get; set; } = string.Empty;
    public MigrationSessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
}
