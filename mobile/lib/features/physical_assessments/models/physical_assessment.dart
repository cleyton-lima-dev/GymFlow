class PhysicalAssessment {
  const PhysicalAssessment({
    required this.id,
    required this.studentId,
    required this.assessmentDate,
    required this.weightKg,
    required this.heightCm,
    required this.bodyFatPercentage,
    required this.chestCm,
    required this.waistCm,
    required this.abdomenCm,
    required this.hipCm,
    required this.rightArmCm,
    required this.leftArmCm,
    required this.rightThighCm,
    required this.leftThighCm,
    required this.rightCalfCm,
    required this.leftCalfCm,
    required this.notes,
    required this.nextAssessmentDate,
    required this.isReassessmentDue,
  });

  final String id;
  final String studentId;

  final DateTime assessmentDate;

  final double weightKg;
  final double heightCm;
  final double? bodyFatPercentage;

  final double? chestCm;
  final double? waistCm;
  final double? abdomenCm;
  final double? hipCm;

  final double? rightArmCm;
  final double? leftArmCm;

  final double? rightThighCm;
  final double? leftThighCm;

  final double? rightCalfCm;
  final double? leftCalfCm;

  final String? notes;

  final DateTime nextAssessmentDate;
  final bool isReassessmentDue;

  factory PhysicalAssessment.fromJson(
      Map<String, dynamic> json,
      ) {
    return PhysicalAssessment(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      assessmentDate: DateTime.parse(
        json['assessmentDate'] as String,
      ),
      weightKg: (json['weightKg'] as num).toDouble(),
      heightCm: (json['heightCm'] as num).toDouble(),
      bodyFatPercentage:
      (json['bodyFatPercentage'] as num?)?.toDouble(),
      chestCm: (json['chestCm'] as num?)?.toDouble(),
      waistCm: (json['waistCm'] as num?)?.toDouble(),
      abdomenCm: (json['abdomenCm'] as num?)?.toDouble(),
      hipCm: (json['hipCm'] as num?)?.toDouble(),
      rightArmCm: (json['rightArmCm'] as num?)?.toDouble(),
      leftArmCm: (json['leftArmCm'] as num?)?.toDouble(),
      rightThighCm:
      (json['rightThighCm'] as num?)?.toDouble(),
      leftThighCm:
      (json['leftThighCm'] as num?)?.toDouble(),
      rightCalfCm:
      (json['rightCalfCm'] as num?)?.toDouble(),
      leftCalfCm:
      (json['leftCalfCm'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
      nextAssessmentDate: DateTime.parse(
        json['nextAssessmentDate'] as String,
      ),
      isReassessmentDue:
      json['isReassessmentDue'] as bool,
    );
  }
}
