class PhysicalAccessCredential {
  const PhysicalAccessCredential({
    required this.id,
    required this.studentId,
    required this.type,
    required this.providerKey,
    required this.externalIdentifier,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String studentId;
  final int type;
  final String providerKey;
  final String externalIdentifier;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;

  factory PhysicalAccessCredential.fromJson(
      Map<String, dynamic> json,
      ) {
    final updatedAtValue =
    json['updatedAt'] as String?;

    return PhysicalAccessCredential(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      type: json['type'] as int,
      providerKey: json['providerKey'] as String,
      externalIdentifier:
      json['externalIdentifier'] as String,
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(
        json['createdAt'] as String,
      ),
      updatedAt: updatedAtValue == null
          ? null
          : DateTime.parse(updatedAtValue),
    );
  }
}
