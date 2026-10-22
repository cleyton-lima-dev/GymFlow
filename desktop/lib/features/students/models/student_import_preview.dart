class StudentImportDuplicateCandidate {
  const StudentImportDuplicateCandidate({
    required this.name,
    required this.matchBy,
    this.studentId,
    this.sourceRowNumber,
    this.email,
    this.phone,
    this.isArchived = false,
  });

  final String name;
  final String matchBy;
  final String? studentId;
  final int? sourceRowNumber;
  final String? email;
  final String? phone;
  final bool isArchived;

  factory StudentImportDuplicateCandidate.fromJson(Map<String, dynamic> json) =>
      StudentImportDuplicateCandidate(
        name: json['name'] as String? ?? '',
        matchBy: json['matchBy'] as String? ?? '',
        studentId: json['studentId'] as String?,
        sourceRowNumber: json['sourceRowNumber'] as int?,
        email: json['email'] as String?,
        phone: json['phone'] as String?,
        isArchived: json['isArchived'] as bool? ?? false,
      );
}

class StudentImportPreviewRow {
  const StudentImportPreviewRow({
    required this.rowNumber,
    required this.isValid,
    required this.errors,
    required this.candidates,
  });

  final int rowNumber;
  final bool isValid;
  final List<String> errors;
  final List<StudentImportDuplicateCandidate> candidates;

  factory StudentImportPreviewRow.fromJson(Map<String, dynamic> json) =>
      StudentImportPreviewRow(
        rowNumber: json['rowNumber'] as int,
        isValid: json['isValid'] as bool,
        errors: (json['errors'] as List<dynamic>).cast<String>(),
        candidates: (json['candidates'] as List<dynamic>)
            .map(
              (item) => StudentImportDuplicateCandidate.fromJson(
                Map<String, dynamic>.from(item as Map),
              ),
            )
            .toList(),
      );
}

class StudentImportResultRow {
  const StudentImportResultRow({
    required this.rowNumber,
    required this.status,
    required this.message,
  });

  final int rowNumber;
  final String status;
  final String message;

  factory StudentImportResultRow.fromJson(Map<String, dynamic> json) =>
      StudentImportResultRow(
        rowNumber: json['rowNumber'] as int,
        status: json['status'] as String,
        message: json['message'] as String,
      );
}

class StudentImportPreview {
  const StudentImportPreview({
    required this.batchId,
    required this.alreadyCompleted,
    required this.rows,
    required this.results,
  });

  final String batchId;
  final bool alreadyCompleted;
  final List<StudentImportPreviewRow> rows;
  final List<StudentImportResultRow> results;

  factory StudentImportPreview.fromJson(Map<String, dynamic> json) =>
      StudentImportPreview(
        batchId: json['batchId'] as String,
        alreadyCompleted: json['alreadyCompleted'] as bool? ?? false,
        rows: (json['rows'] as List<dynamic>)
            .map(
              (item) => StudentImportPreviewRow.fromJson(
                Map<String, dynamic>.from(item as Map),
              ),
            )
            .toList(),
        results: (json['results'] as List<dynamic>)
            .map(
              (item) => StudentImportResultRow.fromJson(
                Map<String, dynamic>.from(item as Map),
              ),
            )
            .toList(),
      );
}

class StudentImportResult {
  const StudentImportResult({
    required this.batchId,
    required this.alreadyCompleted,
    required this.rows,
  });

  final String batchId;
  final bool alreadyCompleted;
  final List<StudentImportResultRow> rows;

  factory StudentImportResult.fromJson(Map<String, dynamic> json) =>
      StudentImportResult(
        batchId: json['batchId'] as String,
        alreadyCompleted: json['alreadyCompleted'] as bool? ?? false,
        rows: (json['results'] as List<dynamic>)
            .map(
              (item) => StudentImportResultRow.fromJson(
                Map<String, dynamic>.from(item as Map),
              ),
            )
            .toList(),
      );
}
