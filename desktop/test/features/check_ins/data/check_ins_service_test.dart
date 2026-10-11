import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/data/check_ins_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('getHistory sends student and period filters', () async {
    final client = MockClient((request) async {
      expect(request.method, 'GET');
      expect(request.url.path, '/api/check-ins');

      expect(request.url.queryParameters['studentId'], 'student-1');

      expect(request.url.queryParameters['fromDate'], '2026-10-01');

      expect(request.url.queryParameters['toDate'], '2026-10-11');

      expect(request.url.queryParameters['page'], '2');

      expect(request.url.queryParameters['pageSize'], '20');

      return http.Response(
        jsonEncode({
          'items': [],
          'page': 2,
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

    final service = CheckInsService(apiClient);

    final result = await service.getHistory(
      studentId: 'student-1',
      fromDate: DateTime(2026, 10, 1),
      toDate: DateTime(2026, 10, 11),
      page: 2,
      pageSize: 20,
    );

    expect(result.page, 2);
    expect(result.items, isEmpty);

    apiClient.close();
  });

  test('createManual sends idempotency request and student', () async {
    final client = MockClient((request) async {
      expect(request.method, 'POST');
      expect(request.url.path, '/api/check-ins/manual');

      final body = jsonDecode(request.body) as Map<String, dynamic>;

      expect(body['requestId'], '11111111-1111-4111-8111-111111111111');

      expect(body['studentId'], 'student-1');

      return http.Response(
        jsonEncode({
          'id': 'check-in-1',
          'studentId': 'student-1',
          'studentName': 'Aluno Teste',
          'requestId': '11111111-1111-4111-8111-111111111111',
          'source': 1,
          'physicalAccessEventId': null,
          'recordedByUserId': 'admin-1',
          'occurredAt': '2026-10-11T03:00:00Z',
          'createdAt': '2026-10-11T03:00:00Z',
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final apiClient = ApiClient(
      baseUrl: 'http://localhost:5137/',
      client: client,
    );

    final service = CheckInsService(apiClient);

    await service.createManual(
      requestId: '11111111-1111-4111-8111-111111111111',
      studentId: 'student-1',
    );

    apiClient.close();
  });
}
