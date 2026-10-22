using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public sealed class MigrationBatch
{
    public Guid Id { get; set; }
    public Guid MigrationSessionId { get; set; }
    public Guid GymId { get; set; }
    public MigrationEntityType EntityType { get; set; }
    public MigrationBatchStatus Status { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "[]";
    public string PreviewJson { get; set; } = "[]";
    public string DecisionsJson { get; set; } = "[]";
    public string ResultJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
}
