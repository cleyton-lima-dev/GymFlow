import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/models/workout_draft.dart';

extension WorkoutDetailsToDraft on WorkoutDetails {
  List<WorkoutDraftDay> toWorkoutDraftDays() {
    final sortedDays = [...days]
      ..sort((first, second) => first.order.compareTo(second.order));

    return sortedDays
        .map((day) {
          final sortedExercises = [...day.exercises]
            ..sort((first, second) => first.order.compareTo(second.order));

          return WorkoutDraftDay(
            id: day.id,
            name: day.name,
            exercises: sortedExercises
                .map(
                  (exercise) => WorkoutDraftExercise(
                    id: exercise.id,
                    exerciseId: exercise.exerciseId,
                    exerciseName: exercise.exerciseName,
                    muscleGroup: exercise.muscleGroup,
                    sets: exercise.sets,
                    repetitions: exercise.repetitions,
                    restSeconds: exercise.restSeconds ?? 0,
                    notes: exercise.notes,
                  ),
                )
                .toList(growable: false),
          );
        })
        .toList(growable: false);
  }
}
