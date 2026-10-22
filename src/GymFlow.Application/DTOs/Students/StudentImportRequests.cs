namespace GymFlow.Application.DTOs.Students;

public sealed record PreviewStudentImportRequest(
    List<StudentImportInputRow>? Rows);

public sealed record StudentImportInputRow(
    int RowNumber,
    string? Name,
    string? Email,
    string? Phone,
    string? BirthDate);

public sealed record ConfirmStudentImportRequest(
    bool? NewStudentsActive,
    List<StudentImportDecision>? Decisions);

public sealed record StudentImportDecision(
    int RowNumber,
    string Action);

public sealed record StudentImportPreviewResponse(
    Guid BatchId,
    bool AlreadyCompleted,
    DateTime ExpiresAt,
    IReadOnlyList<StudentImportPreviewRow> Rows,
    IReadOnlyList<StudentImportResultRow> Results);

public sealed record StudentImportPreviewRow(
    int RowNumber,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<StudentImportDuplicateCandidate> Candidates);

public sealed record StudentImportDuplicateCandidate(
    Guid? StudentId,
    int? SourceRowNumber,
    string Name,
    string? Email,
    string? Phone,
    bool IsArchived,
    string MatchBy);

public sealed record StudentImportResultResponse(
    Guid BatchId,
    bool AlreadyCompleted,
    IReadOnlyList<StudentImportResultRow> Results);

public sealed record StudentImportResultRow(
    int RowNumber,
    string Status,
    string Message);
