import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/physical_assessments/models/paged_physical_assessments_response.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/models/create_physical_assessment_request.dart';

class PhysicalAssessmentsService {
  const PhysicalAssessmentsService(this._apiClient);

  final ApiClient _apiClient;

  Future<PhysicalAssessment?> getLatest(String studentId) {
    return _getLatest('api/students/$studentId/physical-assessments/latest');
  }

  Future<PhysicalAssessment?> getMyLatest() {
    return _getLatest('api/physical-assessments/me/latest');
  }

  Future<PhysicalAssessment?> _getLatest(String path) async {
    try {
      final response = await _apiClient.get(path);

      if (response is! Map) {
        throw const FormatException(
          'Invalid latest physical assessment response.',
        );
      }

      return PhysicalAssessment.fromJson(Map<String, dynamic>.from(response));
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
  }) {
    return _getHistory(
      'api/students/$studentId/physical-assessments',
      page: page,
      pageSize: pageSize,
    );
  }

  Future<PagedPhysicalAssessmentsResponse> getMyHistory({
    int page = 1,
    int pageSize = 20,
  }) {
    return _getHistory(
      'api/physical-assessments/me',
      page: page,
      pageSize: pageSize,
    );
  }

  Future<PagedPhysicalAssessmentsResponse> _getHistory(
    String path, {
    required int page,
    required int pageSize,
  }) async {
    final response = await _apiClient.get(
      '$path?page=$page&pageSize=$pageSize',
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
  }) {
    return _getById(
      'api/students/$studentId/physical-assessments/$assessmentId',
    );
  }

  Future<PhysicalAssessment> getMyById({required String assessmentId}) {
    return _getById('api/physical-assessments/me/$assessmentId');
  }

  Future<PhysicalAssessment> _getById(String path) async {
    final response = await _apiClient.get(path);

    if (response is! Map) {
      throw const FormatException('Invalid physical assessment response.');
    }

    return PhysicalAssessment.fromJson(Map<String, dynamic>.from(response));
  }

  Future<DateTime> getCurrentDate(String studentId) async {
    final response = await _apiClient.get(
      'api/students/$studentId/physical-assessments/current-date',
    );

    if (response is! Map) {
      throw const FormatException('Invalid current gym date response.');
    }

    final json = Map<String, dynamic>.from(response);
    final rawDate = json['currentDate'];

    if (rawDate is! String) {
      throw const FormatException('Invalid current gym date.');
    }

    final parsedDate = DateTime.tryParse(rawDate);

    if (parsedDate == null) {
      throw const FormatException('Invalid current gym date.');
    }

    return DateTime(parsedDate.year, parsedDate.month, parsedDate.day);
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
