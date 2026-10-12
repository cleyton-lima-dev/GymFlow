import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/dashboard/data/dashboard_service.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_metrics.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_view_model.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_page.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_view_model.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

Map<String, dynamic> summary({int overdue = 0}) => {
  'today': '2026-10-12',
  'periodStart': '2026-10-01',
  'expectedRevenue': 0,
  'receivedRevenue': 0,
  'overdueAmount': 0,
  'overdueCharges': overdue,
  'activeEnrollments': 0,
  'expiringEnrollments': 0,
  'activeStudents': 0,
  'studentsWithoutAssessment': 0,
  'reassessmentsDue': 0,
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
              body: SingleChildScrollView(child: DashboardMetrics()),
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
              body: SingleChildScrollView(child: DashboardMetrics()),
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
            of: find.text('Matrículas a vencer'),
            matching: find.byType(InkWell),
          )
          .first;
      expect(tester.widget<InkWell>(passive).onTap, isNull);
      await tester.ensureVisible(find.text('Ver cobranças em atraso →'));
      await tester.tap(find.text('Ver cobranças em atraso →'));
      await tester.pumpAndSettle();
      expect(find.text('overdue'), findsOneWidget);
      router.dispose();
      api.close();
    },
  );

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
}
