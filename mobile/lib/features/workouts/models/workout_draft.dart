import 'package:gymflow/features/workouts/models/create_workout_request.dart';
import 'package:gymflow/features/workouts/models/update_workout_request.dart';

class WorkoutDraftDay {
  WorkoutDraftDay({
    this.id,
    required this.name,
    List<WorkoutDraftExercise>? exercises,
  }) : exercises = exercises ?? [];

  final String? id;
  String name;
  final List<WorkoutDraftExercise> exercises;

  CreateWorkoutDayRequest toRequest({
    required int order,
  }) {
    return CreateWorkoutDayRequest(
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

  UpdateWorkoutDayRequest toUpdateRequest({
    required int order,
  }) {
    return UpdateWorkoutDayRequest(
      id: id,
      name: name.trim(),
      order: order,
      exercises: exercises
          .asMap()
          .entries
          .map(
            (entry) => entry.value.toUpdateRequest(
          order: entry.key + 1,
        ),
      )
          .toList(growable: false),
    );
  }
}

class WorkoutDraftExercise {
  WorkoutDraftExercise({
    this.id,
    required this.exerciseId,
    required this.exerciseName,
    required this.muscleGroup,
    required this.sets,
    required this.repetitions,
    required this.restSeconds,
    this.notes,
  });

  final String? id;

  final String exerciseId;

  // Usados somente pela UI.
  // O backend recebe apenas exerciseId.
  final String exerciseName;
  final String muscleGroup;

  int sets;
  String repetitions;
  int restSeconds;
  String? notes;

  CreateWorkoutExerciseRequest toRequest({
    required int order,
  }) {
    return CreateWorkoutExerciseRequest(
      exerciseId: exerciseId,
      sets: sets,
      repetitions: repetitions.trim(),
      restSeconds: restSeconds,
      notes: _nullableTrimmed(notes),
      order: order,
    );
  }

  UpdateWorkoutExerciseRequest toUpdateRequest({
    required int order,
  }) {
    return UpdateWorkoutExerciseRequest(
      id: id,
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