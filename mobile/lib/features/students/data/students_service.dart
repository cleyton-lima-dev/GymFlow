import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/students/models/paged_students_response.dart';
import 'package:gymflow/features/students/models/student_summary.dart';

class StudentsService {
  const StudentsService(this._apiClient);

  final ApiClient _apiClient;

  Future<PagedStudentsResponse> getStudents({
    String? search,
    bool? isActive,
    int page = 1,
    int pageSize = 20,
  }) async {
    final normalizedSearch = search?.trim();

    final queryParameters = <String, String>{
      'page': page.toString(),
      'pageSize': pageSize.toString(),
      if (normalizedSearch != null && normalizedSearch.isNotEmpty)
        'search': normalizedSearch,
      if (isActive != null) 'isActive': isActive.toString(),
    };

    final query = Uri(queryParameters: queryParameters).query;

    final response = await _apiClient.get('api/students?$query');

    if (response is! Map) {
      throw const FormatException('Invalid students response.');
    }

    return PagedStudentsResponse.fromJson(Map<String, dynamic>.from(response));
  }

  Future<void> createStudent({
    required String name,
    required String email,
    required String password,
    String? phone,
    DateTime? birthDate,
  }) async {
    final normalizedPhone = phone?.trim();

    await _apiClient.post(
      'api/students',
      body: {
        'name': name.trim(),
        'email': email.trim(),
        'password': password,
        'phone': normalizedPhone == null || normalizedPhone.isEmpty
            ? null
            : normalizedPhone,
        'birthDate': birthDate == null ? null : _formatDateOnly(birthDate),
      },
    );
  }

  String _formatDateOnly(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');

    return '$year-$month-$day';
  }

  Future<StudentSummary> getStudentById(String studentId) async {
    final response = await _apiClient.get('api/students/$studentId');

    if (response is! Map) {
      throw const FormatException('Invalid student response.');
    }

    return StudentSummary.fromJson(Map<String, dynamic>.from(response));
  }

  Future<StudentSummary> getMe() async {
    final response = await _apiClient.get('api/students/me');

    if (response is! Map) {
      throw const FormatException('Invalid student profile response.');
    }

    return StudentSummary.fromJson(Map<String, dynamic>.from(response));
  }

  Future<void> updateStudent({
    required String studentId,
    required String name,
    required String email,
    String? phone,
    DateTime? birthDate,
  }) async {
    final normalizedPhone = phone?.trim();

    await _apiClient.put(
      'api/students/$studentId',
      body: {
        'name': name.trim(),
        'email': email.trim(),
        'phone': normalizedPhone == null || normalizedPhone.isEmpty
            ? null
            : normalizedPhone,
        'birthDate': birthDate == null ? null : _formatDateOnly(birthDate),
      },
    );
  }

  Future<void> updateStudentStatus({
    required String studentId,
    required bool isActive,
  }) async {
    await _apiClient.patch(
      'api/students/$studentId/status',
      body: {'isActive': isActive},
    );
  }
}
