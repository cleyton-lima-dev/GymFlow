import 'package:gymflow/features/workout_templates/models/create_workout_template_request.dart';

class WorkoutTemplateDraftDay {
  WorkoutTemplateDraftDay({
    required this.name,
    List<WorkoutTemplateDraftExercise>? exercises,
  }) : exercises = exercises ?? [];

  String name;
  final List<WorkoutTemplateDraftExercise> exercises;

  CreateWorkoutTemplateDayRequest toRequest({
    required int order,
  }) {
    return CreateWorkoutTemplateDayRequest(
      name: name.trim(),
      order: order,
      exercises: exercises
          .asMap()
          .entries
          .map(
            (entry) => entry.value.toRequest(
          order: entry.key + 1,
        ),
      )
          .toList(growable: false),
    );
  }
}

class WorkoutTemplateDraftExercise {
  WorkoutTemplateDraftExercise({
    required this.exerciseId,
    required this.exerciseName,
    required this.muscleGroup,
    required this.sets,
    required this.repetitions,
    required this.restSeconds,
    this.notes,
  });

  final String exerciseId;

  // Estes dois existem apenas para a UI.
  // O backend recebe somente exerciseId.
  final String exerciseName;
  final String muscleGroup;

  int sets;
  String repetitions;
  int restSeconds;
  String? notes;

  CreateWorkoutTemplateExerciseRequest toRequest({
    required int order,
  }) {
    return CreateWorkoutTemplateExerciseRequest(
      exerciseId: exerciseId,
      sets: sets,
      repetitions: repetitions.trim(),
      restSeconds: restSeconds,
      notes: _nullableTrimmed(notes),
      order: order,
    );
  }
}

String? _nullableTrimmed(String? value) {
  final normalized = value?.trim();

  if (normalized == null || normalized.isEmpty) {
    return null;
  }

  return normalized;
}
