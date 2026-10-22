import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/reports/data/reports_service.dart';
import 'package:avelri_gestao/features/reports/presentation/report_presentation.dart';
import 'package:avelri_gestao/features/reports/presentation/reports_page.dart';
import 'package:avelri_gestao/features/reports/presentation/report_table.dart';

import '../../support/management_test_scope.dart';

Map<String, dynamic> response() => {
  'title': 'Alunos',
  'periodBasis': 'Technical backend explanation',
  'timeZone': 'UTC',
  'fromDate': null,
  'toDate': null,
  'columns': [
    'ID aluno',
    'Aluno',
    'Cadastro',
    'RequestId',
    'Matrícula vigente',
  ],
  'rows': [
    [
      '11111111-1111-1111-1111-111111111111',
      'Aluno sintético',
      'Ativo',
      'technical-request',
      'Sim',
    ],
  ],
  'totalCount': 1,
  'page': 1,
  'pageSize': 25,
  'totals': {'Registros': '1'},
};

void main() {
  for (final size in [
    const Size(1024, 720),
    const Size(1280, 720),
    const Size(1366, 768),
    const Size(1920, 1080),
  ]) {
    testWidgets('desktop table navigation and filters at $size', (
      tester,
    ) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final payload = response()
        ..['columns'] = [
          'ID aluno',
          'Aluno',
          'Cadastro',
          'Matrícula vigente',
          'Último contrato',
          'Vencimento do último contrato',
        ]
        ..['rows'] = List.generate(
          25,
          (i) => [
            'hidden-id',
            'Aluno sintético com nome completo $i',
            'Ativo',
            'Sim',
            'Futura',
            '31/12/2026',
          ],
        )
        ..['totalCount'] = 26;
      final api = ApiClient(
        baseUrl: 'https://example.invalid',
        client: MockClient(
          (r) async => http.Response(jsonEncode(payload), 200),
        ),
      );
      await tester.pumpWidget(
        ManagementTestScope(
          api: api,
          child: MaterialApp(
            builder: (context, child) => MediaQuery(
              data: MediaQuery.of(context)
                  .copyWith(textScaler: TextScaler.linear(1.25)),
              child: child!,
            ),
            home: Scaffold(
              body: Row(
                children: [
                  const SizedBox(width: 240),
                  const Expanded(child: ReportsPage()),
                ],
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.text('Matrícula vigente hoje'), findsOneWidget);
      expect(find.text('Sim'), findsNWidgets(25));
      expect(find.text('Futura'), findsNWidgets(25));
      expect(
        tester.getTopLeft(find.text('Exportar CSV')).dy,
        lessThan(tester.getTopLeft(find.byType(ReportTable)).dy),
      );
      final scroll = tester
          .widget<SingleChildScrollView>(
            find.byKey(const ValueKey('reports-table-horizontal')),
          )
          .controller!;
      if (scroll.position.maxScrollExtent > 0) {
        await tester.ensureVisible(find.byTooltip('Próximas colunas'));
        await tester.tap(find.byTooltip('Próximas colunas'));
        await tester.pumpAndSettle();
        expect(scroll.offset, greaterThan(0));
        await tester.tap(find.byTooltip('Colunas anteriores'));
        await tester.pumpAndSettle();
        expect(scroll.offset, 0);
      }
      await tester.ensureVisible(find.byTooltip('Próxima página'));
      expect(
        tester
            .widget<IconButton>(
              find.ancestor(
                of: find.byTooltip('Próxima página'),
                matching: find.byType(IconButton),
              ),
            )
            .onPressed,
        isNotNull,
      );
      expect(tester.takeException(), isNull);
      for (final label in [
        'Financeiro',
        'Matrículas',
        'Frequência',
        'Eventos de acesso',
      ]) {
        final chip = find.widgetWithText(ChoiceChip, label);
        await tester.ensureVisible(chip);
        await tester.tap(chip);
        await tester.pumpAndSettle();
        expect(find.text('Matrícula'), findsNothing);
        expect(find.text('Período: mês da academia'), findsOneWidget);
        expect(tester.takeException(), isNull);
      }
      await tester.pumpWidget(const SizedBox());
      api.close();
    });
  }
  test(
    'presentation hides identifiers without changing source data or CSV',
    () {
      final data = ReportResult.fromJson(response());
      expect(visibleReportColumns(data), [1, 2, 4]);
      expect(data.columns.first, 'ID aluno');
      expect(data.rows.single.first, '11111111-1111-1111-1111-111111111111');
      expect(
        reportCellLabel('Origem', 'OfflineCache'),
        'Offline com permissão salva',
      );
      expect(
        reportCellLabel('Motivo', 'FinancialRestriction'),
        'Restrição financeira',
      );
      expect(
        reportExplanation('attendance', 'charges'),
        contains('não conta como presença'),
      );
    },
  );

  testWidgets('five report choices and only applicable filters are visible', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1600, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final requests = <Uri>[];
    final api = ApiClient(
      baseUrl: 'https://example.invalid',
      client: MockClient((r) async {
        requests.add(r.url);
        return http.Response(jsonEncode(response()), 200);
      }),
    );
    await tester.pumpWidget(
      ManagementTestScope(
        api: api,
        child: const MaterialApp(home: Scaffold(body: ReportsPage())),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(ChoiceChip), findsNWidgets(5));
    expect(find.text('ID aluno'), findsNothing);
    expect(find.text('RequestId'), findsNothing);
    expect(find.text('11111111-1111-1111-1111-111111111111'), findsNothing);
    expect(find.text('Technical backend explanation'), findsNothing);
    expect(find.text('Aluno sintético'), findsOneWidget);
    expect(find.text('Período: mês da academia'), findsNothing);
    expect(find.text('Matrícula'), findsOneWidget);
    final typeY = tester.getTopLeft(find.text('Tipo de relatório')).dy;
    final filtersY = tester.getTopLeft(find.text('Filtros')).dy;
    final resultsY = tester.getTopLeft(find.text('Resultados')).dy;
    final exportY = tester.getTopLeft(find.text('Exportar CSV')).dy;
    expect(
      typeY < filtersY && filtersY < resultsY && resultsY < exportY,
      isTrue,
    );
    expect(exportY < tester.getTopLeft(find.byType(DataTable)).dy, isTrue);
    for (final kind in [
      'Financeiro',
      'Matrículas',
      'Frequência',
      'Eventos de acesso',
    ]) {
      await tester.tap(find.widgetWithText(ChoiceChip, kind));
      await tester.pumpAndSettle();
      expect(find.text('Matrícula'), findsNothing);
      expect(find.text('Período: mês da academia'), findsOneWidget);
      expect(
        find.text('Visão financeira'),
        kind == 'Financeiro' ? findsOneWidget : findsNothing,
      );
      expect(
        find.text('Origem'),
        kind == 'Frequência' ? findsOneWidget : findsNothing,
      );
    }
    await tester.tap(find.widgetWithText(ChoiceChip, 'Financeiro'));
    await tester.pumpAndSettle();
    await tester.tap(find.byType(DropdownButtonFormField<String>).last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Pagamentos').last);
    await tester.pumpAndSettle();
    expect(find.text('Situação'), findsNothing);
    await tester.tap(find.widgetWithText(FilledButton, 'Consultar'));
    await tester.pumpAndSettle();
    expect(requests.last.queryParameters['view'], 'payments');
    expect(requests.last.queryParameters.containsKey('status'), isFalse);
    await tester.pumpWidget(const SizedBox());
    api.close();
  });
}
