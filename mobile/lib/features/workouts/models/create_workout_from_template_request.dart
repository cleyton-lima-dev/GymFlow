import 'package:gymflow/features/workouts/models/create_workout_request.dart';

class CreateWorkoutFromTemplateRequest {
  const CreateWorkoutFromTemplateRequest({
    required this.studentId,
    required this.templateId,
    required this.name,
    required this.description,
    required this.days,
  });

  final String studentId;
  final String templateId;
  final String name;
  final String? description;
  final List<CreateWorkoutDayRequest> days;

  Map<String, dynamic> toJson() {
    return {
      'studentId': studentId,
      'templateId': templateId,
      'name': name,
      'description': description,
      'days': days
          .map(
            (day) => day.toJson(),
      )
          .toList(growable: false),
    };
  }
}