import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/models/paged_students_response.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:avelri_gestao/features/students/models/student_financial_history_item.dart';
import 'package:avelri_gestao/features/students/models/physical_access_credential.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/students/models/physical_access_override.dart';

class StudentsService {
  const StudentsService(this._apiClient);

  final ApiClient _apiClient;

  Future<PagedStudentsResponse> getStudents({
    String? search,
    bool? isActive,
    int? enrollmentFilter,
    int? archiveFilter,
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
      if (enrollmentFilter != null)
        'enrollmentFilter': enrollmentFilter.toString(),
      if (archiveFilter != null)
        'archiveFilter': archiveFilter.toString(),
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

  Future<List<StudentFinancialHistoryItem>>
  getStudentFinancialHistory(
      String studentId,
      ) async {
    final response = await _apiClient.get(
      'api/students/$studentId/financial-history',
    );

    if (response is! List) {
      throw const FormatException(
        'Invalid student financial history response.',
      );
    }

    return response
        .map(
          (item) =>
          StudentFinancialHistoryItem.fromJson(
            Map<String, dynamic>.from(
              item as Map,
            ),
          ),
    )
        .toList();
  }

  Future<PhysicalAccessOverride?>
  getPhysicalAccessOverride(
      String studentId,
      ) async {
    try {
      final response = await _apiClient.get(
        'api/physical-access/overrides/student/$studentId',
      );

      if (response is! Map) {
        throw const FormatException(
          'Invalid physical access override response.',
        );
      }

      return PhysicalAccessOverride.fromJson(
        Map<String, dynamic>.from(response),
      );
    } on ApiException catch (exception) {
      if (exception.statusCode == 404) {
        return null;
      }

      rethrow;
    }
  }

  Future<void> reactivateArchivedStudent({
    required String studentId,
    required String planId,
    required DateTime startDate,
  }) async {
    await _apiClient.post(
      'api/students/$studentId/reactivate',
      body: {
        'planId': planId,
        'startDate': _formatDateOnly(startDate),
      },
    );
  }

  String _formatDateOnly(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');

    return '$year-$month-$day';
  }

  Future<List<PhysicalAccessCredential>>
  getPhysicalAccessCredentials(
      String studentId,
      ) async {
    final response = await _apiClient.get(
      'api/physical-access/credentials/student/$studentId',
    );

    if (response is! List) {
      throw const FormatException(
        'Invalid physical access credentials response.',
      );
    }

    return response
        .map(
          (item) =>
          PhysicalAccessCredential.fromJson(
            Map<String, dynamic>.from(
              item as Map,
            ),
          ),
    )
        .toList();
  }

  Future<PhysicalAccessCredential>
  createPhysicalAccessCredential({
    required String studentId,
    required int type,
    required String providerKey,
    required String externalIdentifier,
  }) async {
    final response = await _apiClient.post(
      'api/physical-access/credentials',
      body: {
        'studentId': studentId,
        'type': type,
        'providerKey': providerKey.trim(),
        'externalIdentifier':
        externalIdentifier.trim(),
      },
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid physical access credential response.',
      );
    }

    return PhysicalAccessCredential.fromJson(
      Map<String, dynamic>.from(response),
    );
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

  Future<void> updatePhysicalAccessCredentialStatus({
    required String credentialId,
    required bool isActive,
  }) async {
    await _apiClient.patch(
      'api/physical-access/credentials/$credentialId/status',
      body: {
        'isActive': isActive,
      },
    );
  }

  Future<void> removePhysicalAccessOverride({
    required String studentId,
  }) async {
    await _apiClient.delete(
      'api/physical-access/overrides/student/$studentId',
    );
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

  Future<PhysicalAccessOverride>
  setPhysicalAccessOverride({
    required String studentId,
    required int type,
    String? reason,
  }) async {
    final normalizedReason = reason?.trim();

    final response = await _apiClient.put(
      'api/physical-access/overrides/student/$studentId',
      body: {
        'type': type,
        'reason': normalizedReason == null ||
            normalizedReason.isEmpty
            ? null
            : normalizedReason,
      },
    );

    if (response is! Map) {
      throw const FormatException(
        'Invalid physical access override response.',
      );
    }

    return PhysicalAccessOverride.fromJson(
      Map<String, dynamic>.from(response),
    );
  }
}
