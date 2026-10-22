import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/reports/data/reports_service.dart';
import 'package:avelri_gestao/features/reports/presentation/reports_page.dart';
import 'package:avelri_gestao/features/reports/presentation/reports_view_model.dart';

import '../../support/management_test_scope.dart';

Map<String, Object?> report({bool empty = false}) => {
  'title': 'Relatório sintético',
  'periodBasis': 'Somente registros reais',
  'timeZone': 'UTC',
  'fromDate': '2026-10-01',
  'toDate': '2026-10-31',
  'columns': ['Aluno', 'Valor'],
  'rows': empty
      ? []
      : [
          ['Aluno sintético', '80,00'],
        ],
  'totalCount': empty ? 0 : 2,
  'page': 1,
  'pageSize': 1,
  'totals': {'Recebido': empty ? '0,00' : '160,00'},
};

void main() {
  test(
    'export and pagination reuse applied filters, server dates and JWT',
    () async {
      final requests = <http.Request>[];
      final api = ApiClient(
        baseUrl: 'https://example.invalid',
        client: MockClient((request) async {
          requests.add(request);
          if (request.url.path.endsWith('/export')) {
            return http.Response.bytes([0xef, 0xbb, 0xbf, 65], 200);
          }
          return http.Response(
            jsonEncode(report()),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
      )..setAccessToken('synthetic-token');
      final model = ReportsViewModel(ReportsService(api));
      final filters = {
        'search': 'ação;@',
        'status': 'paid',
        'view': 'payments',
      };
      await model.load('financial', filters);
      filters['search'] = 'changed outside model';
      await model.goTo(2);
      final bytes = await model.export();
      expect(bytes, [0xef, 0xbb, 0xbf, 65]);
      expect(requests[1].url.queryParameters['page'], '2');
      expect(requests.last.url.path, '/api/reports/financial/export');
      expect(requests.last.url.queryParameters['search'], 'ação;@');
      expect(requests.last.url.queryParameters['status'], 'paid');
      expect(requests.last.url.queryParameters['fromDate'], '2026-10-01');
      expect(requests.last.url.queryParameters['toDate'], '2026-10-31');
      expect(requests.last.url.queryParameters.containsKey('page'), isFalse);
      expect(
        requests.every(
          (r) => r.headers['Authorization'] == 'Bearer synthetic-token',
        ),
        isTrue,
      );
      model.dispose();
      api.close();
    },
  );

  test(
    'export 401 invokes the existing session invalidation handler',
    () async {
      var invalidated = false;
      final api =
          ApiClient(
            baseUrl: 'https://example.invalid',
            client: MockClient((_) async => http.Response('', 401)),
          )..setUnauthorizedHandler(() async {
            invalidated = true;
          });
      await expectLater(
        api.getBytes('/api/reports/students/export'),
        throwsException,
      );
      expect(invalidated, isTrue);
      api.close();
    },
  );

  test(
    'loading, error, retry and export limit are exposed without stale data',
    () async {
      final pending = Completer<http.Response>();
      var calls = 0;
      final api = ApiClient(
        baseUrl: 'https://example.invalid',
        client: MockClient((_) async {
          calls++;
          if (calls == 1) return pending.future;
          if (calls == 3) {
            return http.Response('{"message":"Restrinja os filtros."}', 400);
          }
          return http.Response(jsonEncode(report()), 200);
        }),
      );
      final model = ReportsViewModel(ReportsService(api));
      final load = model.load('students', {});
      expect(model.isLoading, isTrue);
      pending.complete(
        http.Response('internal details should not be shown', 500),
      );
      await load;
      expect(model.result, isNull);
      expect(model.error, isNot(contains('internal details')));
      await model.load('students', {});
      expect(model.error, isNull);
      expect(model.result!.rows.single[1], '80,00');
      expect(await model.export(), isNull);
      expect(model.error, 'Restrinja os filtros.');
      model.dispose();
      api.close();
    },
  );

  testWidgets('report page shows empty state and disables empty exports', (
    tester,
  ) async {
    final api = ApiClient(
      baseUrl: 'https://example.invalid',
      client: MockClient(
        (_) async => http.Response(jsonEncode(report(empty: true)), 200),
      ),
    );
    await tester.pumpWidget(
      ManagementTestScope(
        api: api,
        child: const MaterialApp(home: Scaffold(body: ReportsPage())),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      find.text('Nenhum registro encontrado para os filtros aplicados.'),
      findsOneWidget,
    );
    final export = tester.widget<OutlinedButton>(
      find.widgetWithText(OutlinedButton, 'Exportar CSV'),
    );
    expect(export.onPressed, isNull);
    await tester.pumpWidget(const SizedBox());
    api.close();
  });

  testWidgets('edited filters require consultation before export', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1600, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final api = ApiClient(
      baseUrl: 'https://example.invalid',
      client: MockClient((_) async => http.Response(jsonEncode(report()), 200)),
    );
    await tester.pumpWidget(
      ManagementTestScope(
        api: api,
        child: const MaterialApp(home: Scaffold(body: ReportsPage())),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Aluno sintético'), findsOneWidget);
    await tester.enterText(find.byType(TextField).first, 'novo filtro');
    await tester.pump();
    expect(
      find.text('Filtros alterados. Clique em Consultar antes de exportar.'),
      findsOneWidget,
    );
    expect(
      tester
          .widget<OutlinedButton>(
            find.widgetWithText(OutlinedButton, 'Exportar CSV'),
          )
          .onPressed,
      isNull,
    );
    await tester.pumpWidget(const SizedBox());
    api.close();
  });
}
