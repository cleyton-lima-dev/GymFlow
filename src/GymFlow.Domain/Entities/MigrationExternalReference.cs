using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public sealed class MigrationExternalReference
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public Guid MigrationSessionId { get; set; }
    public string SourceNamespace { get; set; } = string.Empty;
    public MigrationEntityType EntityType { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Guid TargetEntityId { get; set; }
    public DateTime CreatedAt { get; set; }
}
