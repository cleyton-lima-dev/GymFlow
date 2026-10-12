import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/models/physical_access_event.dart';

class PhysicalAccessEventsService {
  const PhysicalAccessEventsService(this._apiClient);

  final ApiClient _apiClient;

  Future<PagedPhysicalAccessEventsResponse> getHistory({
    String? studentId,
    int? decision,
    int? source,
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
      if (decision != null) 'decision': decision.toString(),
      if (source != null) 'source': source.toString(),
      if (fromDate != null) 'fromDate': _formatDateOnly(fromDate),
      if (toDate != null) 'toDate': _formatDateOnly(toDate),
    };

    final query = Uri(queryParameters: queryParameters).query;

    final response = await _apiClient.get('api/physical-access/events?$query');

    if (response is! Map) {
      throw const FormatException('Invalid physical access events response.');
    }

    return PagedPhysicalAccessEventsResponse.fromJson(
      Map<String, dynamic>.from(response),
    );
  }

  String _formatDateOnly(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');

    return '$year-$month-$day';
  }
}
