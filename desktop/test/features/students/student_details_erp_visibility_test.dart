import 'dart:convert';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/presentation/student_details_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../support/management_test_scope.dart';

void main() {
  for (final enabled in [false, true]) {
    testWidgets(
      'student detail ERP actions and requests follow entitlement $enabled',
      (tester) async {
        tester.view.physicalSize = const Size(1800, 1200);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final paths = <String>[];
        final api = ApiClient(
          baseUrl: 'https://synthetic.invalid',
          client: MockClient((request) async {
            final path = request.url.path;
            paths.add(path);
            if (path == '/api/students/synthetic-student') {
              return http.Response(
                jsonEncode({
                  'id': 'synthetic-student',
                  'name': 'Synthetic Student',
                  'email': 'student@example.invalid',
                  'isActive': true,
                  'createdAt': '2026-10-01',
                  'archivedAt': '2026-10-02',
                  'appAccessEnabled': true,
                  'userId': 'synthetic-digital-account',
                }),
                200,
              );
            }
            if (path ==
                '/api/physical-access/overrides/student/synthetic-student') {
              return http.Response('', 204);
            }
            return http.Response('[]', 200);
          }),
        );
        final branding = BrandingController(
          const LocalBrandingRepository({}),
          defaultBranding: avelriDefaultBranding,
        );
        await tester.pumpWidget(
          ManagementTestScope(
            api: api,
            erpEnabled: enabled,
            child: ChangeNotifierProvider.value(
              value: branding,
              child: const MaterialApp(
                home: Scaffold(
                  body: StudentDetailsPage(studentId: 'synthetic-student'),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('Synthetic Student'), findsWidgets);
        expect(find.text('Acesso habilitado'), findsOneWidget);
        expect(
          find.text('Reativar aluno'),
          enabled ? findsOneWidget : findsNothing,
        );
        expect(
          find.text('Histórico financeiro'),
          enabled ? findsOneWidget : findsNothing,
        );
        expect(
          paths.contains('/api/students/synthetic-student/financial-history'),
          enabled,
        );
        expect(paths.where((path) => path.startsWith('/api/plans')), isEmpty);
        expect(
          paths,
          contains(
            '/api/physical-access/credentials/student/synthetic-student',
          ),
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
        branding.dispose();
        api.close();
      },
    );
  }
}
