class UpdateWorkoutRequest {
  const UpdateWorkoutRequest({
    required this.name,
    required this.description,
    required this.days,
  });

  final String name;
  final String? description;
  final List<UpdateWorkoutDayRequest> days;

  Map<String, dynamic> toJson() {
    return {
      'name': name,
      'description': description,
      'days': days
          .map((day) => day.toJson())
          .toList(growable: false),
    };
  }
}

class UpdateWorkoutDayRequest {
  const UpdateWorkoutDayRequest({
    required this.id,
    required this.name,
    required this.order,
    required this.exercises,
  });

  final String? id;
  final String name;
  final int order;
  final List<UpdateWorkoutExerciseRequest> exercises;

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'order': order,
      'exercises': exercises
          .map((exercise) => exercise.toJson())
          .toList(growable: false),
    };
  }
}

class UpdateWorkoutExerciseRequest {
  const UpdateWorkoutExerciseRequest({
    required this.id,
    required this.exerciseId,
    required this.sets,
    required this.repetitions,
    required this.restSeconds,
    required this.notes,
    required this.order,
  });

  final String? id;
  final String exerciseId;
  final int sets;
  final String repetitions;
  final int? restSeconds;
  final String? notes;
  final int order;

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'exerciseId': exerciseId,
      'sets': sets,
      'repetitions': repetitions,
      'restSeconds': restSeconds,
      'notes': notes,
      'order': order,
    };
  }
}
