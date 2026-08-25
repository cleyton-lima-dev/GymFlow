import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/models/create_workout_from_template_request.dart';
import 'package:gymflow/features/workouts/models/create_workout_request.dart';
import 'package:gymflow/features/workouts/models/update_workout_request.dart';
import 'package:gymflow/features/workouts/models/paged_workout_history_response.dart';

class WorkoutsService {
  const WorkoutsService(this._apiClient);

  final ApiClient _apiClient;

  Future<WorkoutDetails?> getCurrentWorkout({
    required String studentId,
  }) async {
    try {
      final response = await _apiClient.get(
        'api/workouts/students/$studentId/current',
      );

      if (response is! Map) {
        throw const FormatException(
          'Invalid current workout response.',
        );
      }

      return WorkoutDetails.fromJson(
        Map<String, dynamic>.from(response),
      );
    } on ApiException catch (exception) {
      if (exception.statusCode == 404) {
        return null;
      }

      rethrow;
    }
  }
  Future<void> createFromTemplate({
    required CreateWorkoutFromTemplateRequest request,
  }) async {
    await _apiClient.post(
      'api/workouts/from-template',
      body: request.toJson(),
    );
  }
  Future<void> createWorkout({
    required CreateWorkoutRequest request,
  }) async {
    await _apiClient.post(
      'api/workouts',
      body: request.toJson(),
    );
  }
  Future<void> updateWorkout({
    required String workoutId,
    required UpdateWorkoutRequest request,
  }) async {
    await _apiClient.put(
      'api/workouts/$workoutId',
      body: request.toJson(),
    );
  }
  Future<PagedWorkoutHistoryResponse> getStudentHistory({
    required String studentId,
    int page = 1,
    int pageSize = 20,
  }) async {
    final response = await _apiClient.get(
      'api/workouts/students/$studentId/history'
          '?page=$page&pageSize=$pageSize',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid workout history response.',
      );
    }

    return PagedWorkoutHistoryResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
}
