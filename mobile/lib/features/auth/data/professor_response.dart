class ProfessorResponse {
  const ProfessorResponse({
    required this.id,
    required this.name,
    required this.email,
    required this.isActive,
    required this.createdAt,
  });

  final String id;
  final String name;
  final String email;
  final bool isActive;
  final DateTime createdAt;

  factory ProfessorResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    return ProfessorResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(
        json['createdAt'] as String,
      ),
    );
  }
}
