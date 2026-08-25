import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';
import 'package:gymflow/features/workouts/models/workout_draft.dart';

extension WorkoutTemplateDetailsToDraft
on WorkoutTemplateDetails {
  List<WorkoutDraftDay> toWorkoutDraftDays() {
    final sortedDays = [...days]
      ..sort(
            (first, second) =>
            first.order.compareTo(second.order),
      );

    return sortedDays
        .map(
          (day) {
        final sortedExercises = [...day.exercises]
          ..sort(
                (first, second) =>
                first.order.compareTo(second.order),
          );

        return WorkoutDraftDay(
          name: day.name,
          exercises: sortedExercises
              .map(
                (exercise) =>
                WorkoutDraftExercise(
                  exerciseId:
                  exercise.exerciseId,
                  exerciseName:
                  exercise.exerciseName,
                  muscleGroup:
                  exercise.muscleGroup,
                  sets: exercise.sets,
                  repetitions:
                  exercise.repetitions,
                  restSeconds:
                  exercise.restSeconds,
                  notes: exercise.notes,
                ),
          )
              .toList(growable: false),
        );
      },
    )
        .toList(growable: false);
  }
}
