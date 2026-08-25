class CreateExerciseRequest {
  const CreateExerciseRequest({
    required this.name,
    required this.muscleGroup,
    this.description,
  });

  final String name;
  final String muscleGroup;
  final String? description;

  Map<String, dynamic> toJson() {
    final normalizedDescription = description?.trim();

    return {
      'name': name.trim(),
      'muscleGroup': muscleGroup.trim(),
      'description':
      normalizedDescription == null ||
          normalizedDescription.isEmpty
          ? null
          : normalizedDescription,
    };
  }
}
