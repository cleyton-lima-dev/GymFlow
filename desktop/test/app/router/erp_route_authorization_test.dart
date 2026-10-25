import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/app/app.dart';
import 'package:avelri_gestao/app/router/app_router.dart';
import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/branding_preview.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/erp_access_policy.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import '../../support/test_session_storage.dart';

const _admin = SessionUser(
  userId: 'synthetic-admin',
  gymId: 'synthetic-gym',
  name: 'Synthetic Admin',
  email: 'admin@example.invalid',
  role: AppRole.admin,
);

class _Harness {
  _Harness(
    this.api,
    this.session,
    this.features,
    this.branding,
    this.navigation,
    this.paths,
    this.setEnabled,
  );
  final ApiClient api;
  final SessionController session;
  final ErpFeatureController features;
  final BrandingController branding;
  final AppRouter navigation;
  final List<String> paths;
  final void Function(bool) setEnabled;

  static Future<_Harness> create({
    bool enabled = false,
    int featureStatus = 200,
    bool loadFeatures = true,
    BrandingPreview? preview,
    bool startAuthenticated = true,
  }) async {
    final paths = <String>[];
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((request) async {
        final path = request.url.path;
        paths.add(path);
        if (path == '/api/gym-features') {
          return http.Response(
            jsonEncode({'erpEnabled': enabled}),
            featureStatus,
          );
        }
        if (path == '/api/students') {
          return http.Response(
            jsonEncode({
              'items': [],
              'page': 1,
              'pageSize': 20,
              'totalCount': 0,
              'totalPages': 1,
            }),
            200,
          );
        }
        if (path == '/api/dashboard/operational') {
          return http.Response(
            jsonEncode({
              'today': '2026-10-19',
              'activeStudents': 7,
              'studentsWithoutAssessment': 1,
              'reassessmentsDue': 2,
            }),
            200,
          );
        }
        if (path == '/api/dashboard') {
          return http.Response(
            jsonEncode({
              'today': '2026-10-19',
              'periodStart': '2026-10-01',
              'expectedRevenue': 10,
              'receivedRevenue': 5,
              'overdueAmount': 5,
              'overdueCharges': 1,
              'activeEnrollments': 7,
              'expiringEnrollments': 1,
              'activeStudents': 7,
              'studentsWithoutAssessment': 1,
              'reassessmentsDue': 2,
              'expiringWithinDays': 15,
            }),
            200,
          );
        }
        if (path == '/api/reports/students') {
          return http.Response(
            jsonEncode({
              'title': 'Relatório sintético',
              'periodBasis': 'Synthetic',
              'timeZone': 'UTC',
              'columns': ['Aluno'],
              'rows': [],
              'totalCount': 0,
              'page': 1,
              'pageSize': 20,
              'totals': {},
            }),
            200,
          );
        }
        return http.Response('[]', 200);
      }),
    );
    final session = SessionController(
      TokenStorage(
        const FlutterSecureStorage(),
        restorationGuard: TestSessionRestorationGuard(),
      ),
      AuthService(api),
      api,
    );
    if (startAuthenticated) {
      await session.startSession(user: _admin, token: 'synthetic-token');
    } else {
      await session.invalidateSession();
    }
    final features = ErpFeatureController(api);
    api.setErpAccessDeniedHandler(features.denyCurrentSession);
    final branding = BrandingController(
      const LocalBrandingRepository({}),
      defaultBranding: avelriDefaultBranding,
      preview: preview,
    );
    if (startAuthenticated) {
      await branding.loadForGym(_admin.gymId);
      if (loadFeatures) await features.loadForGym(_admin.gymId);
    }
    return _Harness(
      api,
      session,
      features,
      branding,
      AppRouter(session, branding, features),
      paths,
      (value) {
        enabled = value;
      },
    );
  }

  Widget get app => AvelriGestaoApp(
    sessionController: session,
    brandingController: branding,
    erpFeatureController: features,
    apiClient: api,
    router: navigation.router,
  );

  void dispose() {
    navigation.dispose();
    features.dispose();
    session.dispose();
    branding.dispose();
    api.close();
  }
}

void main() {
  late FlutterSecureStoragePlatform original;
  setUp(() {
    original = FlutterSecureStoragePlatform.instance;
    FlutterSecureStoragePlatform.instance = TestFlutterSecureStoragePlatform(
      {},
    );
  });
  tearDown(() {
    FlutterSecureStoragePlatform.instance = original;
  });

  void desktopSize(WidgetTester tester) {
    tester.view.physicalSize = const Size(1800, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
  }

  test('branding previews preserve the authenticated session, entitlements, and API calls', () async {
    for (final preview in BrandingPreview.values) {
      final harness = await _Harness.create(enabled: true, preview: preview);

      expect(harness.session.user, _admin);
      expect(harness.session.user!.gymId, _admin.gymId);
      expect(harness.branding.isLoadedForGym(_admin.gymId), isTrue);
      expect(harness.branding.branding, preview.branding);
      expect(harness.features.isEnabledForGym(_admin.gymId), isTrue);
      expect(harness.paths, ['/api/gym-features']);

      harness.dispose();
    }
  });

  testWidgets('login remains Avelri neutral with and without an AC preview', (
    tester,
  ) async {
    desktopSize(tester);
    for (final preview in <BrandingPreview?>[
      BrandingPreview.acPowerGym,
      null,
    ]) {
      final harness = await _Harness.create(
        preview: preview,
        startAuthenticated: false,
      );
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();

      final loginContext = tester.element(find.text('Bem-vindo de volta'));
      expect(
        Theme.of(loginContext).colorScheme.primary,
        avelriDefaultBranding.primaryColor,
      );
      expect(Theme.of(loginContext).brightness, Brightness.light);
      expect(
        tester.widget<MaterialApp>(find.byType(MaterialApp)).title,
        avelriDefaultBranding.displayName,
      );
      expect(
        find.textContaining('Pré-visualização local do tema:'),
        findsNothing,
      );

      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    }
  });

  testWidgets('logout returns to the neutral login despite an AC preview', (
    tester,
  ) async {
    desktopSize(tester);
    final harness = await _Harness.create(
      enabled: true,
      preview: BrandingPreview.acPowerGym,
    );
    await tester.pumpWidget(harness.app);
    await tester.pumpAndSettle();
    expect(
      Theme.of(tester.element(find.text('Dashboard gerencial'))).brightness,
      Brightness.dark,
    );

    await harness.session.logout();
    await tester.pumpAndSettle();

    final loginContext = tester.element(find.text('Bem-vindo de volta'));
    expect(
      Theme.of(loginContext).colorScheme.primary,
      avelriDefaultBranding.primaryColor,
    );
    expect(Theme.of(loginContext).brightness, Brightness.light);
    expect(
      tester.widget<MaterialApp>(find.byType(MaterialApp)).title,
      avelriDefaultBranding.displayName,
    );
    expect(
      find.textContaining('Pré-visualização local do tema:'),
      findsNothing,
    );

    await tester.pumpWidget(const SizedBox.shrink());
    harness.dispose();
  });

  testWidgets('preview notice appears only when a preview is selected', (
    tester,
  ) async {
    desktopSize(tester);
    for (final preview in <BrandingPreview?>[
      BrandingPreview.acPowerGym,
      BrandingPreview.avelri,
      null,
    ]) {
      final harness = await _Harness.create(preview: preview);
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();

      if (preview == null) {
        expect(
          find.textContaining('Pré-visualização local do tema:'),
          findsNothing,
        );
      } else {
        expect(
          find.text('Pré-visualização local do tema: ${preview.label}'),
          findsOneWidget,
        );
        final dashboardContext = tester.element(
          find.text('Dashboard gerencial'),
        );
        expect(
          Theme.of(dashboardContext).brightness,
          preview == BrandingPreview.acPowerGym
              ? Brightness.dark
              : Brightness.light,
        );
      }

      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    }
  });

  for (final featureStatus in [200, 404, 500]) {
    testWidgets(
      'financial links and all direct ERP routes are denied after feature status $featureStatus',
      (tester) async {
        desktopSize(tester);
        final harness = await _Harness.create(featureStatus: featureStatus);
        harness.navigation.router.go('/students');
        await tester.pumpWidget(harness.app);
        await tester.pumpAndSettle();
        expect(find.text('Planos'), findsNothing);
        expect(find.text('Matrículas'), findsNothing);
        expect(find.text('Financeiro'), findsNothing);
        expect(find.text('Cadastrar aluno'), findsOneWidget);
        expect(find.text('Check-in'), findsOneWidget);
        expect(find.text('Controle de acesso'), findsOneWidget);
        harness.paths.clear();
        for (final path in [
          '/plans',
          '/plans/new',
          '/plans/edit',
          '/enrollments',
          '/enrollments/new',
          '/finance?status=overdue',
          '/students/import',
          '/students/migration',
        ]) {
          harness.navigation.router.go(path);
          await tester.pumpAndSettle();
          expect(
            harness.navigation.router.routeInformationProvider.value.uri.path,
            '/students',
            reason: path,
          );
          expect(tester.takeException(), isNull, reason: path);
        }
        expect(harness.paths.where(ErpAccessPolicy.isErpApiPath), isEmpty);
        await tester.pumpWidget(const SizedBox.shrink());
        harness.dispose();
      },
    );
  }

  testWidgets('enabled Admin can navigate directly to ERP pages', (
    tester,
  ) async {
    desktopSize(tester);
    final harness = await _Harness.create(enabled: true);
    await tester.pumpWidget(harness.app);
    await tester.pumpAndSettle();
    for (final path in [
      '/plans',
      '/plans/new',
      '/enrollments',
      '/enrollments/new',
      '/finance',
      '/students/import',
      '/students/migration',
    ]) {
      harness.navigation.router.go(path);
      await tester.pumpAndSettle();
      expect(
        harness.navigation.router.routeInformationProvider.value.uri.path,
        path,
      );
      expect(tester.takeException(), isNull, reason: path);
    }
    harness.navigation.router.go('/plans/edit');
    await tester.pumpAndSettle();
    expect(
      harness.navigation.router.routeInformationProvider.value.uri.path,
      '/plans',
    );
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox.shrink());
    harness.dispose();
  });

  testWidgets(
    'financial report kinds are hidden while operational reports remain available',
    (tester) async {
      desktopSize(tester);
      final harness = await _Harness.create();
      harness.navigation.router.go('/reports');
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();
      expect(find.widgetWithText(ChoiceChip, 'Financeiro'), findsNothing);
      expect(find.widgetWithText(ChoiceChip, 'Matrículas'), findsNothing);
      for (final label in ['Alunos', 'Frequência', 'Eventos de acesso']) {
        expect(find.widgetWithText(ChoiceChip, label), findsOneWidget);
      }
      expect(harness.paths, contains('/api/reports/students'));
      expect(find.text('Matrícula'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    },
  );

  testWidgets(
    'ERP rejection removes the current financial page and cached permission',
    (tester) async {
      desktopSize(tester);
      final harness = await _Harness.create(enabled: true);
      harness.navigation.router.go('/finance');
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();
      harness.features.denyCurrentSession();
      await tester.pumpAndSettle();
      expect(
        harness.navigation.router.routeInformationProvider.value.uri.path,
        '/students',
      );
      expect(find.text('Financeiro'), findsNothing);
      expect(harness.features.isEnabledForGym(_admin.gymId), isFalse);
      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    },
  );

  testWidgets(
    'same-gym new account cannot keep a financial route or old feature state',
    (tester) async {
      desktopSize(tester);
      final harness = await _Harness.create(enabled: true);
      harness.navigation.router.go('/finance');
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();
      await harness.session.startSession(
        user: const SessionUser(
          userId: 'second-user',
          gymId: 'synthetic-gym',
          name: 'Second Admin',
          email: 'second@example.invalid',
          role: AppRole.admin,
        ),
        token: 'second-token',
      );
      await tester.pumpAndSettle();
      expect(harness.features.isEnabledForGym(_admin.gymId), isFalse);
      expect(
        harness.navigation.router.routeInformationProvider.value.uri.path,
        '/students',
      );
      expect(find.text('Financeiro'), findsNothing);
      harness.navigation.router.go('/dashboard');
      await tester.pumpAndSettle();
      expect(harness.paths.last, '/api/dashboard/operational');
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    },
  );

  testWidgets(
    'traditional management remains usable while entitlement is unknown',
    (tester) async {
      desktopSize(tester);
      final harness = await _Harness.create(loadFeatures: false);
      harness.navigation.router.go('/students');
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();
      expect(harness.features.isLoadedForGym(_admin.gymId), isFalse);
      expect(
        harness.navigation.router.routeInformationProvider.value.uri.path,
        '/students',
      );
      expect(find.text('Cadastrar aluno'), findsOneWidget);
      expect(find.text('Financeiro'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    },
  );

  testWidgets('periodic refresh revokes existing ERP navigation', (
    tester,
  ) async {
    desktopSize(tester);
    final harness = await _Harness.create(enabled: true);
    harness.navigation.router.go('/finance');
    await tester.pumpWidget(harness.app);
    await tester.pumpAndSettle();
    harness.setEnabled(false);
    await tester.pump(const Duration(minutes: 1));
    await tester.pumpAndSettle();
    expect(harness.features.isEnabledForGym(_admin.gymId), isFalse);
    expect(
      harness.navigation.router.routeInformationProvider.value.uri.path,
      '/students',
    );
    expect(find.text('Financeiro'), findsNothing);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox.shrink());
    harness.dispose();
  });

  testWidgets(
    'logout blocks direct financial navigation without making ERP requests',
    (tester) async {
      desktopSize(tester);
      final harness = await _Harness.create(enabled: true);
      await tester.pumpWidget(harness.app);
      await tester.pumpAndSettle();
      await harness.session.logout();
      await tester.pumpAndSettle();
      harness.paths.clear();
      harness.navigation.router.go('/finance');
      await tester.pumpAndSettle();
      expect(
        harness.navigation.router.routeInformationProvider.value.uri.path,
        '/login',
      );
      expect(harness.paths, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      harness.dispose();
    },
  );

  for (final replaceSession in [false, true]) {
    testWidgets(
      'root financial popup is dismissed on ${replaceSession ? 'account replacement' : 'ERP revocation'}',
      (tester) async {
        desktopSize(tester);
        final harness = await _Harness.create(enabled: true);
        harness.navigation.router.go('/students');
        await tester.pumpWidget(harness.app);
        await tester.pumpAndSettle();
        final pageContext = tester.element(find.text('Gestão de alunos'));
        unawaited(
          showDialog<void>(
            context: pageContext,
            builder: (_) => const AlertDialog(
              title: Text('Synthetic financial form'),
              content: Text('Synthetic plan price'),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('Synthetic financial form'), findsOneWidget);
        if (replaceSession) {
          await harness.session.startSession(
            user: const SessionUser(
              userId: 'replacement',
              gymId: 'synthetic-gym',
              name: 'Replacement',
              email: 'replacement@example.invalid',
              role: AppRole.admin,
            ),
            token: 'replacement-token',
          );
        } else {
          harness.features.denyCurrentSession();
        }
        await tester.pumpAndSettle();
        expect(find.text('Synthetic financial form'), findsNothing);
        expect(find.text('Synthetic plan price'), findsNothing);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
        harness.dispose();
      },
    );
  }
}
