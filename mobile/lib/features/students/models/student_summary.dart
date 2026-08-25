class StudentSummary {
  const StudentSummary({
    required this.id,
    required this.name,
    required this.email,
    required this.phone,
    required this.birthDate,
    required this.isActive,
    required this.createdAt,
  });

  final String id;
  final String name;
  final String email;
  final String? phone;
  final DateTime? birthDate;
  final bool isActive;
  final DateTime createdAt;

  factory StudentSummary.fromJson(Map<String, dynamic> json) {
    final birthDateValue = json['birthDate'] as String?;

    return StudentSummary(
      id: json['id'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      phone: json['phone'] as String?,
      birthDate: birthDateValue == null
          ? null
          : DateTime.parse(birthDateValue),
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
