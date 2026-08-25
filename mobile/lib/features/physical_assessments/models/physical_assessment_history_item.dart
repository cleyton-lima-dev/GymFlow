class PhysicalAssessmentHistoryItem {
  const PhysicalAssessmentHistoryItem({
    required this.id,
    required this.assessmentDate,
    required this.weightKg,
    required this.heightCm,
    required this.bodyFatPercentage,
    required this.nextAssessmentDate,
    required this.isReassessmentDue,
  });

  final String id;
  final DateTime assessmentDate;

  final double weightKg;
  final double heightCm;
  final double? bodyFatPercentage;

  final DateTime nextAssessmentDate;
  final bool isReassessmentDue;

  factory PhysicalAssessmentHistoryItem.fromJson(
      Map<String, dynamic> json,
      ) {
    return PhysicalAssessmentHistoryItem(
      id: json['id'] as String,
      assessmentDate: DateTime.parse(
        json['assessmentDate'] as String,
      ),
      weightKg: (json['weightKg'] as num).toDouble(),
      heightCm: (json['heightCm'] as num).toDouble(),
      bodyFatPercentage:
      (json['bodyFatPercentage'] as num?)?.toDouble(),
      nextAssessmentDate: DateTime.parse(
        json['nextAssessmentDate'] as String,
      ),
      isReassessmentDue:
      json['isReassessmentDue'] as bool,
    );
  }
}
