using GymFlow.Application.DTOs.Migrations;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Tests.Services;

public sealed class MigrationBatchResultTotalsTests
{
    [Fact]
    public void RequiresHumanReview_WhenPreviewHasConflictAmbiguityErrorOrFinancialResolution()
    {
        Assert.False(MigrationBatchResultTotalsCalculator.RequiresHumanReview(Preview()));
        Assert.True(MigrationBatchResultTotalsCalculator.RequiresHumanReview(Preview(candidates:
            [new(null, 3, "Possível duplicidade", "Mesmo e-mail", false)])));
        Assert.True(MigrationBatchResultTotalsCalculator.RequiresHumanReview(Preview(candidates:
        [
            new(Guid.NewGuid(), null, "Aluno A", "E-mail", false),
            new(Guid.NewGuid(), null, "Aluno B", "Telefone", false)
        ])));
        Assert.True(MigrationBatchResultTotalsCalculator.RequiresHumanReview(Preview(
            isValid: false, errors: ["Campo obrigatório ausente."])));
        Assert.True(MigrationBatchResultTotalsCalculator.RequiresHumanReview(Preview(
            requiresFinancialResolution: true)));
    }

    [Fact]
    public void Calculate_CountsEachSourceLineOnceAndAllowsReviewedToOverlap()
    {
        var rows = new[]
        {
            Result(2, "Imported", false),
            Result(3, "Linked", true),
            Result(4, "AlreadyLinked", true),
            Result(4, "AlreadyLinked", true),
            Result(5, "Ignored", true),
            Result(6, "Error", true)
        };

        var totals = MigrationBatchResultTotalsCalculator.Calculate(rows);

        Assert.Equal(new MigrationBatchTotals(2, 2, 1, 4), totals);
        Assert.Equal(5, totals.SuccessCount + totals.IgnoredCount + totals.ErrorCount);
    }

    [Fact]
    public void Calculate_ReportsReviewedAsUnavailableForLegacyRowsWithoutReviewEvidence()
    {
        var totals = MigrationBatchResultTotalsCalculator.Calculate([Result(2, "Imported", null)]);

        Assert.Equal(new MigrationBatchTotals(1, 0, 0, null), totals);
    }

    [Fact]
    public void Calculate_RejectsOneSourceLineInMultipleOutcomeCategories()
    {
        Assert.Throws<InvalidOperationException>(() => MigrationBatchResultTotalsCalculator.Calculate(
            [Result(2, "Imported", false), Result(2, "Ignored", false)]));
    }

    private static MigrationPreviewRow Preview(bool isValid = true, IReadOnlyList<string>? errors = null,
        IReadOnlyList<MigrationCandidate>? candidates = null, bool requiresFinancialResolution = false) =>
        new(2, isValid, errors ?? [], candidates ?? [], new Dictionary<string, string?>(), requiresFinancialResolution);

    private static MigrationBatchResult Result(int rowNumber, string status, bool? requiredHumanReview) =>
        new(MigrationEntityType.Students, rowNumber, status, "synthetic", null,
            RequiredHumanReview: requiredHumanReview);
}
