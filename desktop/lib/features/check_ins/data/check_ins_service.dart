import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/models/paged_check_ins_response.dart';

class CheckInsService {
  const CheckInsService(this._apiClient);

  final ApiClient _apiClient;

  Future<PagedCheckInsResponse> getHistory({
    String? studentId,
    DateTime? fromDate,
    DateTime? toDate,
    int page = 1,
    int pageSize = 20,
  }) async {
    final queryParameters = <String, String>{
      'page': page.toString(),
      'pageSize': pageSize.toString(),
      if (studentId != null && studentId.trim().isNotEmpty)
        'studentId': studentId.trim(),
      if (fromDate != null) 'fromDate': _formatDateOnly(fromDate),
      if (toDate != null) 'toDate': _formatDateOnly(toDate),
    };

    final query = Uri(queryParameters: queryParameters).query;

    final response = await _apiClient.get('api/check-ins?$query');

    if (response is! Map) {
      throw const FormatException('Invalid check-ins response.');
    }

    return PagedCheckInsResponse.fromJson(Map<String, dynamic>.from(response));
  }

  Future<void> createManual({
    required String requestId,
    required String studentId,
  }) async {
    await _apiClient.post(
      'api/check-ins/manual',
      body: {'requestId': requestId, 'studentId': studentId},
    );
  }

  String _formatDateOnly(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');

    final month = date.month.toString().padLeft(2, '0');

    final day = date.day.toString().padLeft(2, '0');

    return '$year-$month-$day';
  }
}
