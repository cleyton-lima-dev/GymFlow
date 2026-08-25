import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/physical_assessments/models/paged_physical_assessments_response.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/models/create_physical_assessment_request.dart';

class PhysicalAssessmentsService {
  const PhysicalAssessmentsService(this._apiClient);

  final ApiClient _apiClient;

  Future<PhysicalAssessment?> getLatest(
      String studentId,
      ) async {
    try {
      final response = await _apiClient.get(
        'api/students/$studentId/physical-assessments/latest',
      );

      if (response is! Map) {
        throw const FormatException(
          'Invalid latest physical assessment response.',
        );
      }

      return PhysicalAssessment.fromJson(
        Map<String, dynamic>.from(response),
      );
    } on ApiException catch (exception) {
      if (exception.statusCode == 404) {
        return null;
      }

      rethrow;
    }
  }

  Future<PagedPhysicalAssessmentsResponse> getHistory({
    required String studentId,
    int page = 1,
    int pageSize = 20,
  }) async {
    final response = await _apiClient.get(
      'api/students/$studentId/physical-assessments'
          '?page=$page&pageSize=$pageSize',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid physical assessments history response.',
      );
    }

    return PagedPhysicalAssessmentsResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  Future<PhysicalAssessment> getById({
    required String studentId,
    required String assessmentId,
  }) async {
    final response = await _apiClient.get(
      'api/students/$studentId/physical-assessments/$assessmentId',
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid physical assessment response.',
      );
    }

    return PhysicalAssessment.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
  Future<void> create({
    required String studentId,
    required CreatePhysicalAssessmentRequest request,
  }) async {
    await _apiClient.post(
      'api/students/$studentId/physical-assessments',
      body: request.toJson(),
    );
  }
}
