namespace GymFlow.Domain.Entities;

public sealed class StudentImportBatch
{
    public Guid Id { get; set; }
    public Guid GymId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int State { get; set; }
    public string? PayloadJson { get; set; }
    public string? PreviewJson { get; set; }
    public string? ResultJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
