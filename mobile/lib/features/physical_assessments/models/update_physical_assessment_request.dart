class UpdatePhysicalAssessmentRequest {
  const UpdatePhysicalAssessmentRequest({
    required this.weightKg,
    required this.heightCm,
    this.bodyFatPercentage,
    this.chestCm,
    this.waistCm,
    this.abdomenCm,
    this.hipCm,
    this.rightArmCm,
    this.leftArmCm,
    this.rightThighCm,
    this.leftThighCm,
    this.rightCalfCm,
    this.leftCalfCm,
    this.notes,
  });

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

  Map<String, dynamic> toJson() {
    return {
      'weightKg': weightKg,
      'heightCm': heightCm,
      'bodyFatPercentage': bodyFatPercentage,
      'chestCm': chestCm,
      'waistCm': waistCm,
      'abdomenCm': abdomenCm,
      'hipCm': hipCm,
      'rightArmCm': rightArmCm,
      'leftArmCm': leftArmCm,
      'rightThighCm': rightThighCm,
      'leftThighCm': leftThighCm,
      'rightCalfCm': rightCalfCm,
      'leftCalfCm': leftCalfCm,
      'notes': notes,
    };
  }
}
