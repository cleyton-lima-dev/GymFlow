import 'dart:convert';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/branding_repository.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/presentation/student_details_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../support/management_test_scope.dart';

void main() {
  testWidgets(
    'Admin confirms invite revocation and Desktop sends enabled=false',
    (tester) async {
      final requests = <String, Map<String, dynamic>>{};
      final readPaths = <String>[];
      final apiClient = ApiClient(
        baseUrl: 'https://synthetic.example/',
        client: MockClient((request) async {
          readPaths.add(request.url.path);
          if (request.method == 'GET' &&
              request.url.path == '/api/students/student-1') {
            return httpResponse({
              'id': 'student-1',
              'name': 'Aluno de teste',
              'email': 'pending@example.test',
              'phone': null,
              'birthDate': null,
              'isActive': true,
              'createdAt': '2026-01-01T00:00:00Z',
              'enrollmentStatus': null,
              'planName': null,
              'enrollmentEndDate': null,
              'archivedAt': null,
              'appAccessEnabled': true,
              'userId': null,
            });
          }
          if (request.method == 'GET' &&
              request.url.path.endsWith('/financial-history')) {
            return httpResponse([]);
          }
          if (request.method == 'GET' &&
              request.url.path.endsWith(
                '/physical-access/credentials/student/student-1',
              )) {
            return httpResponse([]);
          }
          if (request.method == 'GET' &&
              request.url.path.endsWith(
                '/physical-access/overrides/student/student-1',
              )) {
            return httpResponse({'message': 'not found'}, statusCode: 404);
          }
          if (request.method == 'POST' &&
              request.url.path == '/api/students/student-1/app-access') {
            final body = jsonDecode(request.body) as Map<String, dynamic>;
            requests[request.url.path] = body;
            return httpResponse({
              'enabled': body['enabled'],
              'token': null,
              'expiresAt': null,
            });
          }
          throw StateError(
            'Unexpected request ${request.method} ${request.url}',
          );
        }),
      );
      final brandingController = BrandingController(
        _TestBrandingRepository(),
        defaultBranding: const GymBranding(
          id: 'test',
          displayName: 'Avelri Test',
          primaryColor: Colors.blue,
          secondaryColor: Colors.lightBlue,
          backgroundColor: Colors.white,
          brightness: Brightness.light,
        ),
      );

      await tester.pumpWidget(
        ManagementTestScope(
          api: apiClient,
          erpEnabled: false,
          child: MultiProvider(
            providers: [
              ChangeNotifierProvider<BrandingController>.value(
                value: brandingController,
              ),
            ],
            child: const MaterialApp(
              home: Scaffold(body: StudentDetailsPage(studentId: 'student-1')),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Convite pendente de ativação'), findsOneWidget);
      expect(find.text('Revogar convite'), findsOneWidget);
      await tester.ensureVisible(find.text('Revogar convite').last);
      await tester.tap(find.text('Revogar convite').last);
      await tester.pumpAndSettle();

      expect(
        find.text(
          'O convite deixará de funcionar imediatamente. O cadastro do aluno será mantido e você poderá emitir outro convite depois.',
        ),
        findsOneWidget,
      );
      expect(requests, isEmpty);
      await tester.tap(find.text('Manter convite'));
      await tester.pumpAndSettle();
      expect(requests, isEmpty);

      await tester.ensureVisible(find.text('Revogar convite').last);
      await tester.tap(find.text('Revogar convite').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Revogar convite').last);
      await tester.pumpAndSettle();

      expect(requests['/api/students/student-1/app-access'], {
        'enabled': false,
      });
      expect(readPaths, isNot(contains('/api/students/student-1/financial-history')));
      expect(
        find.text('Convite revogado. O cadastro do aluno foi mantido.'),
        findsOneWidget,
      );
      expect(find.text('Acesso desabilitado'), findsOneWidget);
      expect(find.text('Habilitar acesso'), findsOneWidget);
    },
  );
}

http.Response httpResponse(Object? body, {int statusCode = 200}) =>
    http.Response(
      jsonEncode(body),
      statusCode,
      headers: {'content-type': 'application/json'},
    );

class _TestBrandingRepository implements BrandingRepository {
  @override
  Future<GymBranding?> getByGymId(String gymId) async => null;
}
