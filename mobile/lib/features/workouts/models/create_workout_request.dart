class CreateWorkoutRequest {
  const CreateWorkoutRequest({
    required this.studentId,
    required this.name,
    required this.description,
    required this.days,
  });

  final String studentId;
  final String name;
  final String? description;
  final List<CreateWorkoutDayRequest> days;

  Map<String, dynamic> toJson() {
    return {
      'studentId': studentId,
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

class CreateWorkoutDayRequest {
  const CreateWorkoutDayRequest({
    required this.name,
    required this.order,
    required this.exercises,
  });

  final String name;
  final int order;
  final List<CreateWorkoutExerciseRequest> exercises;

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

class CreateWorkoutExerciseRequest {
  const CreateWorkoutExerciseRequest({
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
