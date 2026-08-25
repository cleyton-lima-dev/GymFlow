class CreateWorkoutTemplateRequest {
  const CreateWorkoutTemplateRequest({
    required this.name,
    required this.description,
    required this.days,
  });

  final String name;
  final String? description;
  final List<CreateWorkoutTemplateDayRequest> days;

  Map<String, dynamic> toJson() {
    return {
      'name': name,
      'description': description,
      'days': days
          .map(
            (day) => day.toJson(),
      )
          .toList(growable: false),
    };
  }
}

class CreateWorkoutTemplateDayRequest {
  const CreateWorkoutTemplateDayRequest({
    required this.name,
    required this.order,
    required this.exercises,
  });

  final String name;
  final int order;
  final List<CreateWorkoutTemplateExerciseRequest> exercises;

  Map<String, dynamic> toJson() {
    return {
      'name': name,
      'order': order,
      'exercises': exercises
          .map(
            (exercise) => exercise.toJson(),
      )
          .toList(growable: false),
    };
  }
}

class CreateWorkoutTemplateExerciseRequest {
  const CreateWorkoutTemplateExerciseRequest({
    required this.exerciseId,
    required this.sets,
    required this.repetitions,
    required this.restSeconds,
    required this.notes,
    required this.order,
  });

  final String exerciseId;
  final int sets;
  final String repetitions;
  final int restSeconds;
  final String? notes;
  final int order;

  Map<String, dynamic> toJson() {
    return {
      'exerciseId': exerciseId,
      'sets': sets,
      'repetitions': repetitions,
      'restSeconds': restSeconds,
      'notes': notes,
      'order': order,
    };
  }
}
