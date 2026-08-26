class WorkoutExecutionResponse {
  const WorkoutExecutionResponse({
    required this.id,
    required this.workoutDayId,
    required this.completedAt,
  });

  final String id;
  final String workoutDayId;
  final DateTime completedAt;

  factory WorkoutExecutionResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    return WorkoutExecutionResponse(
      id: json['id'] as String,
      workoutDayId: json['workoutDayId'] as String,
      completedAt: DateTime.parse(
        json['completedAt'] as String,
      ),
    );
  }
}
