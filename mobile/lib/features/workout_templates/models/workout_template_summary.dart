class WorkoutTemplateSummary {
  const WorkoutTemplateSummary({
    required this.id,
    required this.name,
    required this.description,
    required this.isActive,
    required this.createdAt,
  });

  final String id;
  final String name;
  final String? description;
  final bool isActive;
  final DateTime createdAt;

  factory WorkoutTemplateSummary.fromJson(
      Map<String, dynamic> json,
      ) {
    return WorkoutTemplateSummary(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String?,
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(
        json['createdAt'] as String,
      ),
    );
  }
}
