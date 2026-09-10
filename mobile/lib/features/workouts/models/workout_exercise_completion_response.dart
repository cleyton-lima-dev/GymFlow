class WorkoutExerciseCompletionResponse {
  const WorkoutExerciseCompletionResponse({
    required this.workoutDayId,
    required this.workoutExerciseId,
    required this.isCompleted,
    required this.completedExercises,
    required this.totalExercises,
  });

  final String workoutDayId;
  final String workoutExerciseId;
  final bool isCompleted;
  final int completedExercises;
  final int totalExercises;

  factory WorkoutExerciseCompletionResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    return WorkoutExerciseCompletionResponse(
      workoutDayId: json['workoutDayId'] as String,
      workoutExerciseId: json['workoutExerciseId'] as String,
      isCompleted: json['isCompleted'] as bool,
      completedExercises: (json['completedExercises'] as num).toInt(),
      totalExercises: (json['totalExercises'] as num).toInt(),
    );
  }
}
