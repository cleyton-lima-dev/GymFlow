import 'dart:convert';

import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/app/theme/space_branding.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:avelri_gestao/features/students/presentation/students_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
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

  testWidgets('Alunos fits desktop widths and uses cards when narrow', (
    tester,
  ) async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid/',
      client: MockClient((request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response(jsonEncode({'erpEnabled': true}), 200);
        }
        if (request.url.path == '/api/students') {
          return http.Response(
            jsonEncode({
              'items': [_student],
              'page': 1,
              'pageSize': 20,
              'totalCount': 1,
              'totalPages': 1,
            }),
            200,
          );
        }
        return http.Response('not found', 404);
      }),
    );
    addTearDown(api.close);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final session = SessionController(
      TokenStorage(
        const FlutterSecureStorage(),
        restorationGuard: TestSessionRestorationGuard(),
      ),
      AuthService(api),
      api,
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
    final features = ErpFeatureController(api);
    await features.loadForGym('tenant-a');
    final branding = BrandingController(
      const LocalBrandingRepository({
        '11111111-1111-1111-1111-111111111111': acPowerGymBranding,
        '91f3fdb7-22b9-43ee-8a10-5e411c44cfbe': spaceBranding,
      }),
      defaultBranding: avelriDefaultBranding,
    );
    final router = GoRouter(
      initialLocation: '/students',
      routes: [
        GoRoute(
          path: '/students',
          builder: (context, state) => const Scaffold(
            body: Row(
              children: [
                SizedBox(width: 240),
                Expanded(child: StudentsPage()),
              ],
            ),
          ),
          routes: [
            GoRoute(
              path: ':id',
              builder: (context, state) => const Scaffold(
                body: Center(child: Text('Detalhe sintético do aluno')),
              ),
            ),
          ],
        ),
      ],
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<ApiClient>.value(value: api),
          ChangeNotifierProvider<SessionController>.value(value: session),
          ChangeNotifierProvider<ErpFeatureController>.value(value: features),
          ChangeNotifierProvider<BrandingController>.value(value: branding),
        ],
        child: Consumer<BrandingController>(
          builder: (context, currentBranding, _) => MaterialApp.router(
            theme: AppTheme.fromBranding(currentBranding.branding),
            routerConfig: router,
          ),
        ),
      ),
    );

    final sizes = [
      const Size(1280, 720),
      const Size(1366, 768),
      const Size(1920, 1080),
      const Size(900, 650),
    ];
    for (final preset in [
      avelriDefaultBranding,
      acPowerGymBranding,
      spaceBranding,
    ]) {
      await branding.loadForGym(preset.id);
      await tester.pumpAndSettle();
      for (final size in sizes) {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1;
        await tester.pumpAndSettle();

        final usesCards = size.width == 900;
        expect(
          find.byKey(
            ValueKey(
              usesCards ? 'students-compact-list' : 'students-responsive-table',
            ),
          ),
          findsOneWidget,
          reason: '$size',
        );
        expect(find.text('Synthetic Student With a Long Name'), findsOneWidget);
        expect(
          find.textContaining('student.with.a.long.address@example.invalid'),
          findsOneWidget,
        );
        expect(
          find.textContaining('Synthetic Monthly Plan With an Extended Name'),
          findsOneWidget,
        );
        if (usesCards) {
          expect(
            find.byKey(const ValueKey('students-compact-card-student-1')),
            findsOneWidget,
            reason: '$size',
          );
        } else {
          expect(find.text('TELEFONE'), findsOneWidget, reason: '$size');
          expect(find.text('MATRÍCULA'), findsOneWidget, reason: '$size');
          expect(find.text('VENCIMENTO'), findsOneWidget, reason: '$size');
        }
        final exception = tester.takeException();
        expect(
          exception,
          isNull,
          reason: '${preset.displayName} $size: $exception',
        );
      }
    }

    tester.view.physicalSize = const Size(900, 650);
    await tester.pumpAndSettle();
    await tester.tap(
      find.byKey(const ValueKey('students-compact-card-student-1')),
    );
    await tester.pumpAndSettle();
    expect(find.text('Detalhe sintético do aluno'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    session.dispose();
    features.dispose();
    branding.dispose();
  });
}

final _student = {
  'id': 'student-1',
  'name': 'Synthetic Student With a Long Name',
  'email': 'student.with.a.long.address@example.invalid',
  'phone': '+55 11 99999-1234',
  'isActive': true,
  'createdAt': '2026-10-19',
  'enrollmentStatus': 1,
  'planName': 'Synthetic Monthly Plan With an Extended Name',
  'enrollmentEndDate': '2030-01-31',
};
