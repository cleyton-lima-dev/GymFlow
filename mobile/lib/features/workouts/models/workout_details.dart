class WorkoutDetails {
  const WorkoutDetails({
    required this.id,
    required this.studentId,
    required this.sourceWorkoutTemplateId,
    required this.name,
    required this.description,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
    required this.days,
  });

  final String id;
  final String studentId;
  final String? sourceWorkoutTemplateId;

  final String name;
  final String? description;

  final bool isActive;

  final DateTime createdAt;
  final DateTime? updatedAt;

  final List<WorkoutDayDetails> days;

  factory WorkoutDetails.fromJson(Map<String, dynamic> json) {
    final rawDays = json['days'];

    if (rawDays is! List) {
      throw const FormatException('Invalid workout days.');
    }

    return WorkoutDetails(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      sourceWorkoutTemplateId: json['sourceWorkoutTemplateId'] as String?,
      name: json['name'] as String,
      description: json['description'] as String?,
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
      days: rawDays
          .map(
            (day) => WorkoutDayDetails.fromJson(
              Map<String, dynamic>.from(day as Map),
            ),
          )
          .toList(growable: false),
    );
  }
}

class WorkoutDayDetails {
  const WorkoutDayDetails({
    required this.id,
    required this.name,
    required this.order,
    required this.exercises,
    required this.completedToday,
    required this.lastCompletedAt,
    required this.lastCompletedAtUtcOffsetMinutes,
  });

  final String id;
  final String name;
  final int order;

  final List<WorkoutExerciseDetails> exercises;

  final bool completedToday;
  final DateTime? lastCompletedAt;
  final int? lastCompletedAtUtcOffsetMinutes;

  factory WorkoutDayDetails.fromJson(Map<String, dynamic> json) {
    final rawExercises = json['exercises'];

    if (rawExercises is! List) {
      throw const FormatException('Invalid workout exercises.');
    }

    return WorkoutDayDetails(
      id: json['id'] as String,
      name: json['name'] as String,
      order: (json['order'] as num).toInt(),
      exercises: rawExercises
          .map(
            (exercise) => WorkoutExerciseDetails.fromJson(
              Map<String, dynamic>.from(exercise as Map),
            ),
          )
          .toList(growable: false),
      completedToday: json['completedToday'] as bool,
      lastCompletedAt: json['lastCompletedAt'] == null
          ? null
          : DateTime.parse(json['lastCompletedAt'] as String),
      lastCompletedAtUtcOffsetMinutes:
          json['lastCompletedAtUtcOffsetMinutes'] as int?,
    );
  }
}

class WorkoutExerciseDetails {
  const WorkoutExerciseDetails({
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
  final int? restSeconds;

  final String? notes;

  final int order;

  factory WorkoutExerciseDetails.fromJson(Map<String, dynamic> json) {
    return WorkoutExerciseDetails(
      id: json['id'] as String,
      exerciseId: json['exerciseId'] as String,
      exerciseName: json['exerciseName'] as String,
      muscleGroup: json['muscleGroup'] as String,
      sets: (json['sets'] as num).toInt(),
      repetitions: json['repetitions'] as String,
      restSeconds: json['restSeconds'] == null
          ? null
          : (json['restSeconds'] as num).toInt(),
      notes: json['notes'] as String?,
      order: (json['order'] as num).toInt(),
    );
  }
}
