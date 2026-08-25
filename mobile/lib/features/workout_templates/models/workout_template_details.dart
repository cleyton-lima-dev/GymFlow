class WorkoutTemplateDetails {
  const WorkoutTemplateDetails({
    required this.id,
    required this.name,
    required this.description,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
    required this.days,
  });

  final String id;
  final String name;
  final String? description;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final List<WorkoutTemplateDay> days;

  factory WorkoutTemplateDetails.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawDays = json['days'];

    if (rawDays is! List) {
      throw const FormatException(
        'Invalid workout template days.',
      );
    }

    return WorkoutTemplateDetails(
      id: json['id'] as String,
      name: json['name'] as String,
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
      days: rawDays
          .map(
            (day) => WorkoutTemplateDay.fromJson(
          Map<String, dynamic>.from(day as Map),
        ),
      )
          .toList(growable: false),
    );
  }
}

class WorkoutTemplateDay {
  const WorkoutTemplateDay({
    required this.id,
    required this.name,
    required this.order,
    required this.exercises,
  });

  final String id;
  final String name;
  final int order;
  final List<WorkoutTemplateExercise> exercises;

  factory WorkoutTemplateDay.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawExercises = json['exercises'];

    if (rawExercises is! List) {
      throw const FormatException(
        'Invalid workout template exercises.',
      );
    }

    return WorkoutTemplateDay(
      id: json['id'] as String,
      name: json['name'] as String,
      order: (json['order'] as num).toInt(),
      exercises: rawExercises
          .map(
            (exercise) =>
            WorkoutTemplateExercise.fromJson(
              Map<String, dynamic>.from(
                exercise as Map,
              ),
            ),
      )
          .toList(growable: false),
    );
  }
}

class WorkoutTemplateExercise {
  const WorkoutTemplateExercise({
    required this.id,
    required this.exerciseId,
    required this.exerciseName,
    required this.muscleGroup,
    required this.sets,
    required this.repetitions,
    required this.restSeconds,
    required this.notes,
    required this.order,
  });

  final String id;
  final String exerciseId;
  final String exerciseName;
  final String muscleGroup;

  final int sets;
  final String repetitions;
  final int restSeconds;
  final String? notes;
  final int order;

  factory WorkoutTemplateExercise.fromJson(
      Map<String, dynamic> json,
      ) {
    return WorkoutTemplateExercise(
      id: json['id'] as String,
      exerciseId: json['exerciseId'] as String,
      exerciseName: json['exerciseName'] as String,
      muscleGroup: json['muscleGroup'] as String,
      sets: (json['sets'] as num).toInt(),
      repetitions: json['repetitions'] as String,
      restSeconds:
      (json['restSeconds'] as num).toInt(),
      notes: json['notes'] as String?,
      order: (json['order'] as num).toInt(),
    );
  }
}
