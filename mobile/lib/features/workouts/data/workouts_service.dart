import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/models/create_workout_from_template_request.dart';
import 'package:gymflow/features/workouts/models/create_workout_request.dart';
import 'package:gymflow/features/workouts/models/update_workout_request.dart';
import 'package:gymflow/features/workouts/models/paged_workout_history_response.dart';
import 'package:gymflow/features/workouts/models/workout_execution_response.dart';
import 'package:gymflow/features/workouts/models/workout_exercise_completion_response.dart';

class WorkoutsService {
  const WorkoutsService(this._apiClient);

  final ApiClient _apiClient;

  Future<WorkoutDetails?> getCurrentWorkout({required String studentId}) {
    return _getCurrentWorkout('api/workouts/students/$studentId/current');
  }

  Future<WorkoutDetails?> getMyCurrentWorkout() {
    return _getCurrentWorkout('api/workouts/me/current');
  }

  Future<WorkoutExecutionResponse> completeMyDay({
    required String workoutDayId,
  }) async {
    final response = await _apiClient.post(
      'api/workouts/me/days/$workoutDayId/complete',
      body: const {},
    );

    if (response is! Map) {
      throw const FormatException('Invalid workout execution response.');
    }

    return WorkoutExecutionResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<WorkoutExerciseCompletionResponse> completeMyExercise({
    required String workoutDayId,
    required String workoutExerciseId,
  }) async {
    final response = await _apiClient.post(
      'api/workouts/me/days/$workoutDayId/exercises/$workoutExerciseId/complete',
      body: const {},
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid workout exercise completion response.',
      );
    }

    return WorkoutExerciseCompletionResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<WorkoutExerciseCompletionResponse> uncompleteMyExercise({
    required String workoutDayId,
    required String workoutExerciseId,
  }) async {
    final response = await _apiClient.delete(
      'api/workouts/me/days/$workoutDayId/exercises/$workoutExerciseId/complete',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid workout exercise completion response.',
      );
    }

    return WorkoutExerciseCompletionResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<WorkoutDetails?> _getCurrentWorkout(String path) async {
    try {
      final response = await _apiClient.get(path);

      if (response is! Map) {
        throw const FormatException('Invalid current workout response.');
      }

      return WorkoutDetails.fromJson(Map<String, dynamic>.from(response));
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
    await _apiClient.post('api/workouts/from-template', body: request.toJson());
  }

  Future<void> createWorkout({required CreateWorkoutRequest request}) async {
    await _apiClient.post('api/workouts', body: request.toJson());
  }

  Future<void> updateWorkout({
    required String workoutId,
    required UpdateWorkoutRequest request,
  }) async {
    await _apiClient.put('api/workouts/$workoutId', body: request.toJson());
  }

  Future<PagedWorkoutHistoryResponse> getStudentHistory({
    required String studentId,
    int page = 1,
    int pageSize = 20,
  }) {
    return _getHistory(
      'api/workouts/students/$studentId/history',
      page: page,
      pageSize: pageSize,
    );
  }

  Future<PagedWorkoutHistoryResponse> getMyHistory({
    int page = 1,
    int pageSize = 20,
  }) {
    return _getHistory(
      'api/workouts/me/history',
      page: page,
      pageSize: pageSize,
    );
  }

  Future<PagedWorkoutHistoryResponse> _getHistory(
    String path, {
    required int page,
    required int pageSize,
  }) async {
    final response = await _apiClient.get(
      '$path?page=$page&pageSize=$pageSize',
    );

    if (response is! Map) {
      throw const FormatException('Invalid workout history response.');
    }

    return PagedWorkoutHistoryResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
}
