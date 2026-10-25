import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/app/router/management_shell_page.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/brand_contrast.dart';
import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:avelri_gestao/features/dashboard/data/dashboard_service.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_metrics.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_view_model.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_page.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_view_model.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/app/theme/space_branding.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../support/management_test_scope.dart';

Map<String, dynamic> summary({
  int overdue = 0,
  double received = 0,
  int expiring = 0,
  int missing = 0,
  int reassessments = 0,
}) => {
  'today': '2026-10-12',
  'periodStart': '2026-10-01',
  'expectedRevenue': 0,
  'receivedRevenue': received,
  'overdueAmount': 0,
  'overdueCharges': overdue,
  'activeEnrollments': 0,
  'expiringEnrollments': expiring,
  'activeStudents': 0,
  'studentsWithoutAssessment': missing,
  'reassessmentsDue': reassessments,
  'expiringWithinDays': 15,
};

void main() {
  test(
    'service uses authenticated aggregate endpoint and parses decimal amounts',
    () async {
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient((request) async {
          expect(request.url.path, '/api/dashboard');
          expect(request.headers['Authorization'], 'Bearer synthetic');
          return http.Response(
            jsonEncode({...summary(), 'receivedRevenue': 80.25}),
            200,
          );
        }),
      )..setAccessToken('synthetic');
      final data = await DashboardService(api).load();
      expect(data.receivedRevenue, 80.25);
      expect(data.expiringWithinDays, 15);
      api.close();
    },
  );

  test(
    'view model recovers from error and preserves last successful data',
    () async {
      var fail = true;
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient(
          (_) async => fail
              ? http.Response('', 500)
              : http.Response(jsonEncode(summary()), 200),
        ),
      );
      final model = DashboardViewModel(DashboardService(api));
      await model.load();
      expect(model.error, isNotNull);
      expect(model.summary, isNull);
      fail = false;
      await model.load();
      expect(model.error, isNull);
      expect(model.summary!.activeStudents, 0);
      fail = true;
      await model.load();
      expect(model.summary, isNotNull);
      expect(model.error, isNotNull);
      expect(model.isLoading, isFalse);
      model.dispose();
      api.close();
    },
  );

  test('dispose during request does not notify destroyed model', () async {
    final pending = Completer<http.Response>();
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((_) => pending.future),
    );
    final model = DashboardViewModel(DashboardService(api));
    final load = model.load();
    model.dispose();
    pending.complete(http.Response(jsonEncode(summary()), 200));
    await load;
    api.close();
  });

  testWidgets(
    'loading, zero data and unavailable frequency do not fabricate metrics',
    (tester) async {
      final pending = Completer<http.Response>();
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient((_) => pending.future),
      );
      await tester.pumpWidget(
        Provider.value(
          value: api,
          child: const MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: DashboardMetrics(erpEnabled: true),
              ),
            ),
          ),
        ),
      );
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
      pending.complete(http.Response(jsonEncode(summary()), 200));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Sem movimentação ou cadastros ativos para os indicadores atuais.',
        ),
        findsOneWidget,
      );
      expect(find.textContaining('indicadores indisponíveis'), findsOneWidget);
      expect(tester.takeException(), isNull);
      api.close();
    },
  );

  testWidgets('dashboard error state offers a working retry', (tester) async {
    var requests = 0;
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((_) async {
        requests++;
        return requests == 1
            ? http.Response('', 503)
            : http.Response(jsonEncode(summary()), 200);
      }),
    );
    await tester.pumpWidget(
      Provider.value(
        value: api,
        child: const MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(child: DashboardMetrics()),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('Não foi possível carregar'), findsOneWidget);
    expect(find.text('Tentar novamente'), findsOneWidget);

    await tester.tap(find.text('Tentar novamente'));
    await tester.pumpAndSettle();
    expect(requests, 2);
    expect(find.text('Alunos ativos'), findsOneWidget);
    expect(find.textContaining('Não foi possível carregar'), findsNothing);
    expect(tester.takeException(), isNull);
    api.close();
  });

  testWidgets(
    'overdue action carries filter and passive metrics have no action',
    (tester) async {
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient(
          (_) async => http.Response(jsonEncode(summary(overdue: 2)), 200),
        ),
      );
      final router = GoRouter(
        routes: [
          GoRoute(
            path: '/',
            builder: (_, _) => const Scaffold(
              body: SingleChildScrollView(
                child: DashboardMetrics(erpEnabled: true),
              ),
            ),
          ),
          GoRoute(
            path: '/finance',
            builder: (_, state) =>
                Text(state.uri.queryParameters['status'] ?? 'missing'),
          ),
        ],
      );
      await tester.pumpWidget(
        Provider.value(
          value: api,
          child: MaterialApp.router(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();
      final passive = find
          .ancestor(
            of: find.text('Matrículas vigentes'),
            matching: find.byType(Card),
          )
          .first;
      expect(
        find.descendant(of: passive, matching: find.byType(InkWell)),
        findsNothing,
      );
      final overdue = find
          .ancestor(
            of: find.text('Cobranças vencidas'),
            matching: find.byType(Card),
          )
          .first;
      expect(
        find.descendant(of: overdue, matching: find.byType(InkWell)),
        findsOneWidget,
      );
      await tester.ensureVisible(find.text('Ver cobranças em atraso →'));
      await tester.tap(find.text('Ver cobranças em atraso →'));
      await tester.pumpAndSettle();
      expect(find.text('overdue'), findsOneWidget);
      router.dispose();
      api.close();
    },
  );

  testWidgets('critical dashboard metrics use readable red semantic colors', (
    tester,
  ) async {
    for (final branding in [avelriDefaultBranding, acPowerGymBranding]) {
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient(
          (_) async =>
              http.Response(jsonEncode(summary(overdue: 2, expiring: 3)), 200),
        ),
      );
      final theme = AppTheme.fromBranding(branding);
      await tester.pumpWidget(
        MaterialApp(
          theme: theme,
          home: Provider.value(
            value: api,
            child: const Scaffold(
              body: SingleChildScrollView(
                child: DashboardMetrics(erpEnabled: true),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final riskColor = AvelriSemanticColors.riskFor(theme.brightness);
      expect(riskColor, isNot(branding.primaryColor));
      if (branding == acPowerGymBranding) {
        expect(riskColor, const Color(0xFFFF4D4D));
      }
      expect(
        BrandContrast.contrastRatio(theme.colorScheme.surface, riskColor),
        greaterThanOrEqualTo(4.5),
      );

      for (final title in ['Cobranças vencidas', 'Matrículas a vencer']) {
        expect(find.text(title), findsOneWidget);
        final card = find
            .ancestor(of: find.text(title), matching: find.byType(Card))
            .first;
        final cardWidget = tester.widget<Card>(card);
        final shape = cardWidget.shape! as RoundedRectangleBorder;
        expect(shape.side.color, riskColor, reason: '$title alert border');
        final textWidgets = tester.widgetList<Text>(
          find.descendant(of: card, matching: find.byType(Text)),
        );
        expect(
          textWidgets.any((text) => text.style?.color == riskColor),
          isTrue,
          reason:
              '$title should use the semantic risk color for ${branding.displayName}',
        );
      }

      expect(
        find.textContaining('Saldo de cobranças em atraso'),
        findsOneWidget,
      );
      expect(
        find.textContaining('Vigentes com término nos próximos'),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
      api.close();
    }
  });

  testWidgets('finance page applies initial overdue filter before loading', (
    tester,
  ) async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((_) async => http.Response('[]', 200)),
    );
    tester.view.physicalSize = const Size(1400, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(() => tester.view.resetPhysicalSize());
    addTearDown(() => tester.view.resetDevicePixelRatio());
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider.value(value: api),
          ChangeNotifierProvider(
            create: (_) => BrandingController(
              const LocalBrandingRepository({}),
              defaultBranding: avelriDefaultBranding,
            ),
          ),
        ],
        child: const MaterialApp(
          home: Scaffold(body: ChargesPage(overdueOnly: true)),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final viewContext = tester.element(find.text('Cobranças').first);
    expect(
      viewContext.read<ChargesViewModel>().statusFilter,
      ChargesStatusFilter.overdue,
    );
    expect(tester.takeException(), isNull);
    api.close();
  });

  testWidgets(
    'App-only dashboard loads only operational metrics and hides financial values',
    (tester) async {
      final paths = <String>[];
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient((request) async {
          paths.add(request.url.path);
          return http.Response(
            jsonEncode({
              'today': '2026-10-19',
              'activeStudents': 7,
              'studentsWithoutAssessment': 2,
              'reassessmentsDue': 1,
            }),
            200,
          );
        }),
      );
      await tester.pumpWidget(
        Provider.value(
          value: api,
          child: const MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(child: DashboardMetrics()),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(paths, ['/api/dashboard/operational']);
      expect(find.text('Alunos ativos'), findsOneWidget);
      expect(find.text('Sem avaliação física'), findsOneWidget);
      expect(find.text('Receita prevista no mês'), findsNothing);
      expect(find.text('Cobranças vencidas'), findsNothing);
      expect(find.text('Matrículas vigentes'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      api.close();
    },
  );

  testWidgets('dashboard uses neutral copy without claiming system readiness', (
    tester,
  ) async {
    final api = _dashboardApi();
    final router = _dashboardRouter();
    await tester.pumpWidget(_dashboardHarness(api, router));
    await tester.pumpAndSettle();

    expect(find.text('Painel de gestão'), findsOneWidget);
    expect(
      find.text('Gerencie as rotinas da academia pelo Avelri.'),
      findsOneWidget,
    );
    expect(find.text('Sistema ativo'), findsNothing);
    expect(
      find.text('Sua área de gestão está conectada e pronta para uso.'),
      findsNothing,
    );
    expect(tester.takeException(), isNull);

    router.dispose();
    api.close();
  });

  testWidgets('management shell does not claim the app is online', (
    tester,
  ) async {
    final api = _dashboardApi();
    final router = _dashboardRouter();
    await tester.pumpWidget(_dashboardHarness(api, router));
    await tester.pumpAndSettle();

    expect(find.text('Área administrativa'), findsOneWidget);
    expect(find.text('Online'), findsNothing);
    expect(tester.takeException(), isNull);

    router.dispose();
    api.close();
  });

  testWidgets('dashboard shortcuts navigate with mouse, Enter and Space', (
    tester,
  ) async {
    final api = _dashboardApi();
    final router = _dashboardRouter();
    await tester.pumpWidget(_dashboardHarness(api, router));
    await tester.pumpAndSettle();

    final studentsShortcut = find.byKey(
      const ValueKey('dashboard-shortcut-Alunos'),
    );
    await tester.ensureVisible(studentsShortcut);
    await tester.tap(studentsShortcut);
    await tester.pumpAndSettle();
    expect(find.text('Destino: /students'), findsOneWidget);

    for (final key in [LogicalKeyboardKey.enter, LogicalKeyboardKey.space]) {
      router.go('/dashboard');
      await tester.pumpAndSettle();
      final shortcut = find.byKey(
        const ValueKey('dashboard-shortcut-Check-in'),
      );
      await tester.ensureVisible(shortcut);
      await tester.pumpAndSettle();
      final focusNode = tester.widget<InkWell>(shortcut).focusNode!;
      focusNode.requestFocus();
      await tester.pumpAndSettle();

      expect(focusNode.hasFocus, isTrue);
      final visual = find.descendant(
        of: shortcut,
        matching: find.byType(AnimatedContainer),
      );
      final decoration =
          tester.widget<AnimatedContainer>(visual.first).decoration!
              as BoxDecoration;
      expect((decoration.border! as Border).top.width, 2);

      await tester.sendKeyEvent(key);
      await tester.pumpAndSettle();
      expect(find.text('Destino: /check-in'), findsOneWidget);
    }

    router.dispose();
    api.close();
  });

  testWidgets(
    'four actionable metrics navigate with matching context filters',
    (tester) async {
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient(
          (_) async => http.Response(
            jsonEncode(
              summary(
                received: 125.5,
                expiring: 2,
                missing: 3,
                reassessments: 4,
              ),
            ),
            200,
          ),
        ),
      );
      final router = _dashboardRouter();
      await tester.pumpWidget(_dashboardHarness(api, router));
      await tester.pumpAndSettle();

      final destinations = <(String, String)>[
        (
          'Receita recebida no mês',
          '/finance?status=paid&paidMonth=2026-10-01',
        ),
        ('Matrículas a vencer', '/enrollments?expiringWithinDays=15'),
        ('Sem avaliação física', '/students?assessmentFilter=missing'),
        (
          'Reavaliações pendentes',
          '/students?assessmentFilter=reassessmentDue',
        ),
      ];
      for (final (title, destination) in destinations) {
        router.go('/dashboard');
        await tester.pumpAndSettle();
        final card = find.byKey(ValueKey('dashboard-metric-$title'));
        await tester.ensureVisible(card);
        await tester.tap(card);
        await tester.pumpAndSettle();
        expect(
          find.text('Destino: $destination'),
          findsOneWidget,
          reason: title,
        );
      }

      for (final key in [LogicalKeyboardKey.enter, LogicalKeyboardKey.space]) {
        router.go('/dashboard');
        await tester.pumpAndSettle();
        final card = find.byKey(
          const ValueKey('dashboard-metric-Sem avaliação física'),
        );
        await tester.ensureVisible(card);
        await tester.pumpAndSettle();
        final focusNode = Focus.of(
          tester.element(find.text('Sem avaliação física')),
          createDependency: false,
        );
        focusNode.requestFocus();
        await tester.pumpAndSettle();
        await tester.sendKeyEvent(key);
        await tester.pumpAndSettle();
        expect(
          find.text('Destino: /students?assessmentFilter=missing'),
          findsOneWidget,
        );
      }

      for (final title in [
        'Receita prevista no mês',
        'Matrículas vigentes',
        'Alunos ativos',
      ]) {
        final card = find.byKey(ValueKey('dashboard-metric-$title'));
        expect(
          find.descendant(of: card, matching: find.byType(InkWell)),
          findsNothing,
        );
      }
      router.dispose();
      api.close();
    },
  );

  testWidgets('dashboard grids fit common and narrow Windows sizes', (
    tester,
  ) async {
    final api = _dashboardApi();
    final router = _dashboardRouter();
    final sizes = [
      const Size(1280, 720),
      const Size(1366, 768),
      const Size(1920, 1080),
      const Size(800, 650),
    ];
    addTearDown(() => tester.view.resetPhysicalSize());
    addTearDown(() => tester.view.resetDevicePixelRatio());

    for (final branding in [
      avelriDefaultBranding,
      acPowerGymBranding,
      spaceBranding,
    ]) {
      for (final size in sizes) {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1;
        await tester.pumpWidget(
          _dashboardHarness(api, router, branding: branding),
        );
        await tester.pumpAndSettle();

        expect(
          tester
              .getSize(find.byKey(const ValueKey('dashboard-shortcut-Alunos')))
              .height,
          150,
          reason: 'shortcut height at $size',
        );
        expect(
          tester
              .getSize(
                find.byKey(const ValueKey('dashboard-metric-Alunos ativos')),
              )
              .height,
          230,
          reason: 'metric height at $size',
        );
        expect(
          tester.takeException(),
          isNull,
          reason: '${branding.displayName} layout at $size',
        );
      }
    }

    router.dispose();
    api.close();
  });
}

ApiClient _dashboardApi() => ApiClient(
  baseUrl: 'https://synthetic.invalid',
  client: MockClient(
    (_) async => http.Response(jsonEncode(summary(overdue: 2)), 200),
  ),
);

GoRouter _dashboardRouter() => GoRouter(
  initialLocation: '/dashboard',
  routes: [
    GoRoute(
      path: '/dashboard',
      builder: (context, state) => ManagementShellPage(
        location: state.uri.path,
        child: const DashboardPage(),
      ),
    ),
    for (final path in [
      '/students',
      '/plans',
      '/finance',
      '/enrollments',
      '/check-in',
    ])
      GoRoute(
        path: path,
        builder: (context, state) =>
            Scaffold(body: Text('Destino: ${state.uri}')),
      ),
  ],
);

Widget _dashboardHarness(
  ApiClient api,
  GoRouter router, {
  GymBranding branding = avelriDefaultBranding,
}) => ManagementTestScope(
  api: api,
  child: ChangeNotifierProvider(
    create: (_) => BrandingController(
      const LocalBrandingRepository({}),
      defaultBranding: branding,
    ),
    child: MaterialApp.router(
      theme: AppTheme.fromBranding(branding),
      routerConfig: router,
    ),
  ),
);
