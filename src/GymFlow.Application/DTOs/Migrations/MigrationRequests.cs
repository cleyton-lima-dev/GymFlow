using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Migrations;

public sealed record CreateMigrationSessionRequest(string? Name, string? SourceNamespace);

public sealed record PreviewMigrationBatchRequest(
    MigrationEntityType EntityType,
    List<MigrationRowInput>? Rows);

public sealed record MigrationRowInput(
    int RowNumber,
    string? ExternalId,
    string? Name,
    string? Email,
    string? Phone,
    string? BirthDate,
    string? Price,
    string? DurationMonths,
    string? BillingCycle,
    string? StudentExternalId,
    string? PlanExternalId,
    string? StartDate,
    string? EndDate,
    string? ChargeState,
    string? ChargeDueDate,
    string? EnrollmentExternalId = null,
    string? ChargeExternalId = null);

public sealed record ReviewMigrationBatchRequest(List<MigrationRowDecision>? Decisions, string? Fingerprint = null);

public sealed record MigrationRowDecision(
    int RowNumber,
    string Action,
    Guid? TargetId = null,
    string? FinancialStateResolution = null,
    Guid? ReviewedByUserId = null,
    DateTime? ReviewedAt = null);

public sealed record MigrationBatchPreviewResponse(
    Guid BatchId,
    MigrationEntityType EntityType,
    IReadOnlyList<MigrationPreviewRow> Rows,
    bool AlreadyStaged,
    IReadOnlyList<MigrationRowDecision>? Decisions = null,
    string? Fingerprint = null);

public sealed record MigrationPreviewRow(
    int RowNumber,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<MigrationCandidate> Candidates,
    IReadOnlyDictionary<string, string?> Values,
    bool RequiresFinancialResolution = false,
    Guid? ExistingTargetId = null);

public sealed record MigrationCandidate(
    Guid? TargetId,
    int? SourceRowNumber,
    string Name,
    string Description,
    bool IsArchived);

public sealed record MigrationSessionResponse(
    Guid SessionId,
    string Name,
    string SourceNamespace,
    MigrationSessionStatus Status,
    DateTime ExpiresAt,
    IReadOnlyList<MigrationBatchSummary> Batches,
    bool CanRelease,
    IReadOnlyList<string> ReleaseBlockers,
    IReadOnlyList<MigrationBatchResult> Results);

public sealed record MigrationSessionListItem(
    Guid SessionId,
    string Name,
    string SourceNamespace,
    MigrationSessionStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    int BatchCount);

public sealed record MigrationBatchSummary(
    Guid BatchId,
    MigrationEntityType EntityType,
    int RowCount,
    int ErrorCount,
    int AmbiguousCount,
    string Status,
    string Fingerprint,
    MigrationBatchTotals? Totals = null);

public sealed record MigrationBatchTotals(
    int SuccessCount,
    int IgnoredCount,
    int ErrorCount,
    int? ReviewedCount);

public sealed record MigrationBatchResultDocument(
    IReadOnlyList<MigrationBatchResult> Rows,
    MigrationBatchTotals Totals);

public sealed record MigrationBatchResult(
    MigrationEntityType EntityType,
    int RowNumber,
    string Status,
    string Message,
    Guid? TargetId,
    string? ExternalId = null,
    string? Action = null,
    string? FinancialStateResolution = null,
    Guid? ReviewedByUserId = null,
    DateTime? ReviewedAt = null,
    Guid? EffectiveStudentId = null,
    bool? RequiredHumanReview = null);

public static class MigrationBatchResultTotalsCalculator
{
    public static bool RequiresHumanReview(MigrationPreviewRow preview) =>
        !preview.IsValid || preview.Errors.Count != 0 || preview.Candidates.Count != 0 || preview.RequiresFinancialResolution;

    public static MigrationBatchTotals Calculate(IEnumerable<MigrationBatchResult> results)
    {
        var rows = results.GroupBy(result => result.RowNumber).Select(group =>
        {
            var categorized = group.Select(result => Category(result.Status)).Distinct().ToArray();
            if (categorized.Length != 1)
                throw new InvalidOperationException($"A linha {group.Key} possui resultados finais em categorias incompatíveis.");
            return (Category: categorized[0], Reviewed: group.Any(result => result.RequiredHumanReview == true),
                ReviewUnknown: group.Any(result => result.RequiredHumanReview is null));
        }).ToList();

        var reviewedKnown = rows.Count(row => row.Reviewed);
        var reviewedUnknown = rows.Any(row => !row.Reviewed && row.ReviewUnknown);
        return new(
            rows.Count(row => row.Category == ResultCategory.Success),
            rows.Count(row => row.Category == ResultCategory.Ignored),
            rows.Count(row => row.Category == ResultCategory.Error),
            reviewedUnknown ? null : reviewedKnown);
    }

    private static ResultCategory Category(string status) => status switch
    {
        "Imported" or "Linked" => ResultCategory.Success,
        "Ignored" or "AlreadyLinked" => ResultCategory.Ignored,
        "Error" => ResultCategory.Error,
        _ => throw new InvalidOperationException($"Estado final de migração sem categoria conhecida: {status}.")
    };

    private enum ResultCategory
    {
        Success,
        Ignored,
        Error
    }
}
