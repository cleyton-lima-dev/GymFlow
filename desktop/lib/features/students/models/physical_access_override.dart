class PhysicalAccessOverride {
  const PhysicalAccessOverride({
    required this.id,
    required this.studentId,
    required this.type,
    required this.reason,
    required this.actorUserId,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String studentId;
  final int type;
  final String? reason;
  final String actorUserId;
  final DateTime createdAt;
  final DateTime? updatedAt;

  factory PhysicalAccessOverride.fromJson(
      Map<String, dynamic> json,
      ) {
    final reasonValue =
    json['reason'] as String?;

    final updatedAtValue =
    json['updatedAt'] as String?;

    return PhysicalAccessOverride(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      type: json['type'] as int,
      reason: reasonValue,
      actorUserId:
      json['actorUserId'] as String,
      createdAt: DateTime.parse(
        json['createdAt'] as String,
      ),
      updatedAt: updatedAtValue == null
          ? null
          : DateTime.parse(updatedAtValue),
    );
  }
}
