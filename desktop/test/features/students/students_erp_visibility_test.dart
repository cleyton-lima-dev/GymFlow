import 'dart:convert';

import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:avelri_gestao/features/students/presentation/students_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../support/test_session_storage.dart';

void main() {
  late FlutterSecureStoragePlatform originalPlatform;

  setUp(() {
    originalPlatform = FlutterSecureStoragePlatform.instance;
    FlutterSecureStoragePlatform.instance = TestFlutterSecureStoragePlatform(
      {},
    );
  });

  tearDown(() {
    FlutterSecureStoragePlatform.instance = originalPlatform;
  });

  for (final enabled in [false, true]) {
    testWidgets(
      'ERP student actions are ${enabled ? 'shown' : 'hidden'} when entitlement is $enabled',
      (tester) async {
        tester.view.physicalSize = const Size(1800, 1200);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final apiClient = ApiClient(
          baseUrl: 'https://api.example.invalid/',
          client: MockClient((request) async {
            if (request.url.path == '/api/gym-features') {
              return http.Response(jsonEncode({'erpEnabled': enabled}), 200);
            }
            if (request.url.path == '/api/students') {
              return http.Response(
                jsonEncode({
                  'items': [
                    {
                      'id': 'student',
                      'name': 'Synthetic Student',
                      'email': 'student@example.invalid',
                      'isActive': true,
                      'createdAt': '2026-10-19',
                      'enrollmentStatus': 1,
                      'planName': 'Synthetic ERP Plan',
                      'enrollmentEndDate': '2030-01-31',
                    },
                  ],
                  'page': 1,
                  'pageSize': 20,
                  'totalCount': 0,
                  'totalPages': 1,
                }),
                200,
              );
            }
            return http.Response('not found', 404);
          }),
        );
        final tokens = TokenStorage(
          const FlutterSecureStorage(),
          restorationGuard: TestSessionRestorationGuard(),
        );
        final session = SessionController(
          tokens,
          AuthService(apiClient),
          apiClient,
        );
        await session.startSession(
          user: const SessionUser(
            userId: 'admin',
            gymId: 'tenant-a',
            name: 'Admin',
            email: 'admin@example.invalid',
            role: AppRole.admin,
          ),
          token: 'test-jwt',
        );
        final features = ErpFeatureController(apiClient);
        await features.loadForGym('tenant-a');
        final branding = BrandingController(
          const LocalBrandingRepository({}),
          defaultBranding: avelriDefaultBranding,
        );

        await tester.pumpWidget(
          MultiProvider(
            providers: [
              Provider<ApiClient>.value(value: apiClient),
              ChangeNotifierProvider<SessionController>.value(value: session),
              ChangeNotifierProvider<ErpFeatureController>.value(
                value: features,
              ),
              ChangeNotifierProvider<BrandingController>.value(value: branding),
            ],
            child: const MaterialApp(home: Scaffold(body: StudentsPage())),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.text('Importar CSV/XLSX'),
          enabled ? findsOneWidget : findsNothing,
        );
        expect(
          find.text('Migração universal'),
          enabled ? findsOneWidget : findsNothing,
        );
        expect(find.text('Cadastrar aluno'), findsOneWidget);
        expect(
          find.byKey(const ValueKey('students-assessment-guidance')),
          findsNothing,
        );
        expect(find.text('MATRÍCULA'), enabled ? findsOneWidget : findsNothing);
        expect(find.text('PLANO'), enabled ? findsOneWidget : findsNothing);
        expect(
          find.text('VENCIMENTO'),
          enabled ? findsOneWidget : findsNothing,
        );
        expect(
          find.text('Synthetic ERP Plan'),
          enabled ? findsOneWidget : findsNothing,
        );
        await tester.tap(find.text('Filtros'));
        await tester.pumpAndSettle();
        expect(find.text('Matrícula'), enabled ? findsOneWidget : findsNothing);
        expect(find.text('Arquivamento'), findsOneWidget);
        await tester.tap(find.text('Cancelar'));
        await tester.pumpAndSettle();

        await tester.pumpWidget(const SizedBox.shrink());
        session.dispose();
        features.dispose();
        branding.dispose();
        apiClient.close();
      },
    );
  }

  for (final filter in ['missing', 'reassessmentDue']) {
    testWidgets(
      'assessment guidance is shown only for contextual filter $filter',
      (tester) async {
        tester.view.physicalSize = const Size(1800, 1200);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final apiClient = ApiClient(
          baseUrl: 'https://api.example.invalid/',
          client: MockClient((request) async {
            if (request.url.path == '/api/gym-features') {
              return http.Response(jsonEncode({'erpEnabled': true}), 200);
            }
            if (request.url.path == '/api/students') {
              return http.Response(
                jsonEncode({
                  'items': <Object>[],
                  'page': 1,
                  'pageSize': 20,
                  'totalCount': 0,
                  'totalPages': 0,
                }),
                200,
              );
            }
            return http.Response('not found', 404);
          }),
        );
        final session = SessionController(
          TokenStorage(
            const FlutterSecureStorage(),
            restorationGuard: TestSessionRestorationGuard(),
          ),
          AuthService(apiClient),
          apiClient,
        );
        await session.startSession(
          user: const SessionUser(
            userId: 'admin',
            gymId: 'tenant-a',
            name: 'Admin',
            email: 'admin@example.invalid',
            role: AppRole.admin,
          ),
          token: 'test-jwt',
        );
        final features = ErpFeatureController(apiClient);
        await features.loadForGym('tenant-a');
        final branding = BrandingController(
          const LocalBrandingRepository({}),
          defaultBranding: avelriDefaultBranding,
        );

        await tester.pumpWidget(
          MultiProvider(
            providers: [
              Provider<ApiClient>.value(value: apiClient),
              ChangeNotifierProvider<SessionController>.value(value: session),
              ChangeNotifierProvider<ErpFeatureController>.value(
                value: features,
              ),
              ChangeNotifierProvider<BrandingController>.value(value: branding),
            ],
            child: MaterialApp(
              home: Scaffold(body: StudentsPage(assessmentFilter: filter)),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.byKey(const ValueKey('students-assessment-guidance')),
          findsOneWidget,
        );
        expect(
          find.text(
            'Para registrar ou atualizar avaliações físicas, use o Avelri Mobile no perfil Admin ou Professor.',
          ),
          findsOneWidget,
        );
        expect(
          find.byKey(const ValueKey('student-assessment-filter')),
          findsOneWidget,
        );
        expect(find.byType(OutlinedButton), findsWidgets);

        await tester.pumpWidget(const SizedBox.shrink());
        session.dispose();
        features.dispose();
        branding.dispose();
        apiClient.close();
      },
    );
  }
}
