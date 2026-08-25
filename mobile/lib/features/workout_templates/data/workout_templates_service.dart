import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/workout_templates/models/paged_workout_templates_response.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';
import 'package:gymflow/features/workout_templates/models/create_workout_template_request.dart';

class WorkoutTemplatesService {
  const WorkoutTemplatesService(this._apiClient);

  final ApiClient _apiClient;

  Future<PagedWorkoutTemplatesResponse> getTemplates({
    String? search,
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

    if (isActive != null) {
      queryParameters.add(
        'isActive=$isActive',
      );
    }

    final response = await _apiClient.get(
      'api/workout-templates?${queryParameters.join('&')}',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid workout templates response.',
      );
    }

    return PagedWorkoutTemplatesResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
  Future<WorkoutTemplateDetails> getById(
      String templateId,
      ) async {
    final response = await _apiClient.get(
      'api/workout-templates/$templateId',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid workout template details response.',
      );
    }

    return WorkoutTemplateDetails.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
  Future<void> create(
      CreateWorkoutTemplateRequest request,
      ) async {
    await _apiClient.post(
      'api/workout-templates',
      body: request.toJson(),
    );
  }
  Future<void> update(
      String templateId,
      CreateWorkoutTemplateRequest request,
      ) async {
    await _apiClient.put(
      'api/workout-templates/$templateId',
      body: request.toJson(),
    );
  }
  Future<void> updateStatus({
    required String templateId,
    required bool isActive,
  }) async {
    await _apiClient.patch(
      'api/workout-templates/$templateId/status',
      body: {
        'isActive': isActive,
      },
    );
  }
}
