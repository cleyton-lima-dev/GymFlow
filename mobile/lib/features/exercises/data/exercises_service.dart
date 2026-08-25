import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/exercises/models/paged_exercises_response.dart';
import 'package:gymflow/features/exercises/models/create_exercise_request.dart';
import 'package:gymflow/features/exercises/models/update_exercise_request.dart';

class ExercisesService {
  const ExercisesService(this._apiClient);

  final ApiClient _apiClient;

  Future<PagedExercisesResponse> getExercises({
    String? search,
    String? muscleGroup,
    bool? isActive,
    int page = 1,
    int pageSize = 20,
  }) async {
    final queryParameters = <String>[
      'page=$page',
      'pageSize=$pageSize',
    ];

    final normalizedSearch = search?.trim();

    if (normalizedSearch != null &&
        normalizedSearch.isNotEmpty) {
      queryParameters.add(
        'search=${Uri.encodeQueryComponent(normalizedSearch)}',
      );
    }

    final normalizedMuscleGroup = muscleGroup?.trim();

    if (normalizedMuscleGroup != null &&
        normalizedMuscleGroup.isNotEmpty) {
      queryParameters.add(
        'muscleGroup=${Uri.encodeQueryComponent(normalizedMuscleGroup)}',
      );
    }

    if (isActive != null) {
      queryParameters.add(
        'isActive=$isActive',
      );
    }

    final response = await _apiClient.get(
      'api/exercises?${queryParameters.join('&')}',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid exercises response.',
      );
    }

    return PagedExercisesResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
  Future<void> createExercise({
    required CreateExerciseRequest request,
  }) async {
    await _apiClient.post(
      'api/exercises',
      body: request.toJson(),
    );
  }
  Future<void> updateExercise({
    required String exerciseId,
    required UpdateExerciseRequest request,
  }) async {
    await _apiClient.put(
      'api/exercises/$exerciseId',
      body: request.toJson(),
    );
  }

  Future<void> updateExerciseStatus({
    required String exerciseId,
    required bool isActive,
  }) async {
    await _apiClient.patch(
      'api/exercises/$exerciseId/status',
      body: {
        'isActive': isActive,
      },
    );
  }
}
