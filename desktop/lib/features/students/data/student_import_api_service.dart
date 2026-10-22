import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/models/student_import_preview.dart';

class StudentImportApiService {
  const StudentImportApiService(this._apiClient);

  final ApiClient _apiClient;

  Future<StudentImportPreview> preview(List<Map<String, dynamic>> rows) async {
    final response = await _apiClient.post(
      'api/students/imports/preview',
      body: {'rows': rows},
    );
    if (response is! Map) {
      throw const FormatException('Resposta inválida da prévia de importação.');
    }
    return StudentImportPreview.fromJson(Map<String, dynamic>.from(response));
  }

  Future<StudentImportResult> confirm({
    required String batchId,
    required bool? newStudentsActive,
    required List<Map<String, dynamic>> decisions,
  }) async {
    final response = await _apiClient.post(
      'api/students/imports/$batchId/confirm',
      body: {'newStudentsActive': newStudentsActive, 'decisions': decisions},
    );
    if (response is! Map) {
      throw const FormatException('Resposta inválida da importação.');
    }
    return StudentImportResult.fromJson(Map<String, dynamic>.from(response));
  }
}
