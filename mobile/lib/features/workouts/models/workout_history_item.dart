class WorkoutHistoryItem {
  const WorkoutHistoryItem({
    required this.executionId,
    required this.workoutId,
    required this.workoutName,
    required this.workoutDayId,
    required this.workoutDayName,
    required this.completedAt,
    required this.completedAtUtcOffsetMinutes,
  });

  final String executionId;
  final String workoutId;
  final String workoutName;
  final String workoutDayId;
  final String workoutDayName;
  final DateTime completedAt;
  final int completedAtUtcOffsetMinutes;

  factory WorkoutHistoryItem.fromJson(Map<String, dynamic> json) {
    return WorkoutHistoryItem(
      executionId: json['executionId'] as String,
      workoutId: json['workoutId'] as String,
      workoutName: json['workoutName'] as String,
      workoutDayId: json['workoutDayId'] as String,
      workoutDayName: json['workoutDayName'] as String,
      completedAt: DateTime.parse(json['completedAt'] as String),
      completedAtUtcOffsetMinutes: json['completedAtUtcOffsetMinutes'] as int,
    );
  }
}
