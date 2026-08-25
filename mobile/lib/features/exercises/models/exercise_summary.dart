class ExerciseSummary {
  const ExerciseSummary({
    required this.id,
    required this.name,
    required this.muscleGroup,
    required this.description,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String name;
  final String muscleGroup;
  final String? description;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;

  factory ExerciseSummary.fromJson(
      Map<String, dynamic> json,
      ) {
    return ExerciseSummary(
      id: json['id'] as String,
      name: json['name'] as String,
      muscleGroup: json['muscleGroup'] as String,
      description: json['description'] as String?,
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(
        json['createdAt'] as String,
      ),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(
        json['updatedAt'] as String,
      ),
    );
  }
}
