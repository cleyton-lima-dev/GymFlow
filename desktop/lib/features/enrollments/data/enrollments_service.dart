import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';

class EnrollmentsService {
  const EnrollmentsService(this._apiClient);

  final ApiClient _apiClient;

  Future<List<EnrollmentSummary>> getEnrollments({
    EnrollmentStatus? status,
  }) async {
    final queryParameters = <String, String>{
      if (status != null)
        'status': status.value.toString(),
    };

    final query =
        Uri(queryParameters: queryParameters).query;

    final path = query.isEmpty
        ? 'api/enrollments'
        : 'api/enrollments?$query';

    final response = await _apiClient.get(path);

    if (response is! List) {
      throw const FormatException(
        'Invalid enrollments response.',
      );
    }

    return response
        .map(
          (item) => EnrollmentSummary.fromJson(
        Map<String, dynamic>.from(
          item as Map,
        ),
      ),
    )
        .toList();
  }

  Future<EnrollmentSummary> createEnrollment({
    required String studentId,
    required String planId,
    required DateTime startDate,
  }) async {
    final response = await _apiClient.post(
      'api/enrollments',
      body: {
        'studentId': studentId,
        'planId': planId,
        'startDate': _formatDate(startDate),
      },
    );

    return EnrollmentSummary.fromJson(
      Map<String, dynamic>.from(
        response as Map,
      ),
    );
  }

  Future<void> cancelEnrollment({
    required String enrollmentId,
  }) async {
    await _apiClient.patch(
      'api/enrollments/$enrollmentId/cancel',
      body: {},
    );
  }

  Future<EnrollmentSummary> renewEnrollment({
    required String enrollmentId,
    required String planId,
  }) async {
    final response = await _apiClient.post(
      'api/enrollments/$enrollmentId/renew',
      body: {
        'planId': planId,
      },
    );

    return EnrollmentSummary.fromJson(
      Map<String, dynamic>.from(
        response as Map,
      ),
    );
  }

  static String _formatDate(DateTime date) {
    final year =
    date.year.toString().padLeft(4, '0');
    final month =
    date.month.toString().padLeft(2, '0');
    final day =
    date.day.toString().padLeft(2, '0');

    return '$year-$month-$day';
  }
}
