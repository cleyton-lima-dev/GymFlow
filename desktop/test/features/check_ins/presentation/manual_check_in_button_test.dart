import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/presentation/manual_check_in_button.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  testWidgets('registers manual check-in using a UUID request id', (
    tester,
  ) async {
    var completedCount = 0;
    var studentRequests = 0;
    var checkInRequests = 0;

    final client = MockClient((request) async {
      if (request.method == 'GET' && request.url.path == '/api/students') {
        studentRequests++;

        expect(request.url.queryParameters['archiveFilter'], '1');

        return http.Response(
          jsonEncode({
            'items': [
              {
                'id': 'student-1',
                'name': 'Aluno Teste',
                'email': 'aluno@teste.com',
                'phone': null,
                'birthDate': null,
                'isActive': true,
                'createdAt': '2026-10-01T12:00:00Z',
                'enrollmentStatus': 1,
                'planName': 'Plano Mensal',
                'enrollmentEndDate': '2026-11-01',
                'archivedAt': null,
              },
            ],
            'page': 1,
            'pageSize': 20,
            'totalCount': 1,
            'totalPages': 1,
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      }

      if (request.method == 'POST' &&
          request.url.path == '/api/check-ins/manual') {
        checkInRequests++;

        final body = jsonDecode(request.body) as Map<String, dynamic>;

        expect(body['studentId'], 'student-1');

        final requestId = body['requestId'] as String;

        expect(
          requestId,
          matches(
            RegExp(
              r'^[0-9a-f]{8}-'
              r'[0-9a-f]{4}-'
              r'4[0-9a-f]{3}-'
              r'[89ab][0-9a-f]{3}-'
              r'[0-9a-f]{12}$',
            ),
          ),
        );

        return http.Response(
          jsonEncode({
            'id': 'check-in-1',
            'studentId': 'student-1',
            'studentName': 'Aluno Teste',
            'requestId': requestId,
            'source': 1,
            'physicalAccessEventId': null,
            'recordedByUserId': 'admin-1',
            'occurredAt': '2026-10-11T04:00:00Z',
            'createdAt': '2026-10-11T04:00:00Z',
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      }

      return http.Response('Not found', 404);
    });

    final apiClient = ApiClient(
      baseUrl: 'http://localhost:5137/',
      client: client,
    );

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ManualCheckInButton(
            apiClient: apiClient,
            onCompleted: () async {
              completedCount++;
            },
          ),
        ),
      ),
    );

    await tester.tap(find.text('Check-in manual'));

    await tester.pumpAndSettle();

    expect(find.text('Selecionar aluno'), findsOneWidget);

    expect(find.text('Aluno Teste'), findsOneWidget);

    await tester.tap(find.text('Aluno Teste'));

    await tester.pumpAndSettle();

    expect(find.text('Registrar check-in manual?'), findsOneWidget);

    await tester.tap(find.text('Registrar'));

    await tester.pumpAndSettle();

    expect(studentRequests, 1);

    expect(checkInRequests, 1);

    expect(completedCount, 1);

    expect(
      find.textContaining('Check-in manual de Aluno Teste registrado.'),
      findsOneWidget,
    );

    apiClient.close();
  });
}
