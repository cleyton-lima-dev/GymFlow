import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/data/check_ins_service.dart';
import 'package:avelri_gestao/features/check_ins/presentation/check_ins_view_model.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('student filter is sent to history endpoint', () async {
    final client = MockClient((request) async {
      expect(request.url.path, '/api/check-ins');

      expect(request.url.queryParameters['studentId'], 'student-1');

      return http.Response(
        jsonEncode({
          'items': [],
          'page': 1,
          'pageSize': 20,
          'totalCount': 0,
          'totalPages': 0,
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final apiClient = ApiClient(
      baseUrl: 'http://localhost:5137/',
      client: client,
    );

    final viewModel = CheckInsViewModel(CheckInsService(apiClient));

    await viewModel.updateStudentFilter(
      studentId: 'student-1',
      studentName: 'Aluno Teste',
    );

    expect(viewModel.studentId, 'student-1');

    expect(viewModel.studentName, 'Aluno Teste');

    expect(viewModel.hasStudentFilter, isTrue);

    viewModel.dispose();
    apiClient.close();
  });
}
