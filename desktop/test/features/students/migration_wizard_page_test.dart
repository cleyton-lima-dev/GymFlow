import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/presentation/migration_wizard_page.dart';
import 'package:file_selector_platform_interface/file_selector_platform_interface.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

void main() {
  test('serializes unmapped contractual columns as null fields', () {
    final row = buildMigrationRowPayload(
      2,
      [
        'externalId',
        'studentExternalId',
        'planExternalId',
        'name',
        'price',
        'durationMonths',
        'billingCycle',
        'startDate',
        'endDate',
      ],
      {
        'externalId': 0,
        'studentExternalId': 1,
        'planExternalId': 2,
        'startDate': 3,
        'endDate': 4,
      },
      ['MAT-01', 'ALU-01', 'PLA-01', '2026-10-01', '2027-03-31'],
    );

    expect(row['externalId'], 'MAT-01');
    expect(row['planExternalId'], 'PLA-01');
    expect(row.containsKey('name'), isTrue);
    expect(row['name'], isNull);
    expect(row['price'], isNull);
    expect(row['durationMonths'], isNull);
    expect(row['billingCycle'], isNull);
  });

  test('migration preview revision changes when mapping changes', () {
    const fields = ['externalId', 'name'];
    final firstMapping = {'externalId': 0, 'name': 1};
    final firstRows = [
      buildMigrationRowPayload(2, fields, firstMapping, ['S-1', 'Ana']),
    ];
    final firstRevision = migrationPreviewRevision(
      sessionId: 'session-1',
      entityType: 1,
      fileName: 'students.csv',
      fields: fields,
      mapping: firstMapping,
      rows: firstRows,
    );
    final unchangedRevision = migrationPreviewRevision(
      sessionId: 'session-1',
      entityType: 1,
      fileName: 'students.csv',
      fields: fields,
      mapping: firstMapping,
      rows: firstRows,
    );
    final changedMapping = {'externalId': 1, 'name': 0};
    final changedRows = [
      buildMigrationRowPayload(2, fields, changedMapping, ['S-1', 'Ana']),
    ];
    final changedRevision = migrationPreviewRevision(
      sessionId: 'session-1',
      entityType: 1,
      fileName: 'students.csv',
      fields: fields,
      mapping: changedMapping,
      rows: changedRows,
    );

    expect(unchangedRevision, firstRevision);
    expect(changedRevision, isNot(firstRevision));
  });

  testWidgets('shows all four migration entity types for a saved session', (
    tester,
  ) async {
    final originalSelector = FileSelectorPlatform.instance;
    final selectorMock = _TestFileSelector();
    FileSelectorPlatform.instance = selectorMock;
    addTearDown(() => FileSelectorPlatform.instance = originalSelector);
    final sessionId = '00000000-0000-0000-0000-000000000121';
    final api = ApiClient(
      baseUrl: 'https://example.invalid',
      client: MockClient((request) async {
        if (request.url.path == '/api/migrations') {
          return http.Response(
            jsonEncode([
              {
                'sessionId': sessionId,
                'name': 'TEST 02',
                'sourceNamespace': 'actuar',
                'status': 1,
                'createdAt': '2026-10-19T12:00:00Z',
                'expiresAt': '2099-10-19T12:00:00Z',
                'batchCount': 0,
              },
            ]),
            200,
          );
        }
        if (request.url.path == '/api/migrations/$sessionId') {
          return http.Response(
            jsonEncode({
              'sessionId': sessionId,
              'name': 'TEST 02',
              'sourceNamespace': 'actuar',
              'status': 1,
              'expiresAt': '2099-10-19T12:00:00Z',
              'batches': [],
              'canRelease': false,
              'releaseBlockers': [],
              'results': [],
            }),
            200,
          );
        }
        return http.Response('Not found', 404);
      }),
    );

    await tester.pumpWidget(
      Provider<ApiClient>.value(
        value: api,
        child: const MaterialApp(home: MigrationWizardPage()),
      ),
    );
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Retomar'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Retomar'));
    await tester.pumpAndSettle();

    final selector = tester.widget<DropdownButton<int>>(
      find.byType(DropdownButton<int>),
    );
    expect(find.text('Baixar modelo'), findsOneWidget);
    expect(find.textContaining('Modelo CSV de alunos'), findsOneWidget);
    expect(selector.items, hasLength(4));
    expect(
      selector.items!.map((item) => (item.child as Text).data),
      containsAll([
        'Alunos',
        'Planos',
        'Matrículas e cobrança atual',
        'Situação financeira atual',
      ]),
    );
    await tester.tap(find.text('Alunos').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Planos').last);
    await tester.pumpAndSettle();
    expect(find.textContaining('Modelo CSV de planos'), findsOneWidget);

    await tester.ensureVisible(find.text('Selecionar CSV/XLSX'));
    await tester.tap(find.text('Selecionar CSV/XLSX'));
    await tester.pumpAndSettle();
    expect(find.text('Selecionar CSV/XLSX'), findsOneWidget);

    final modelData = await rootBundle.load(
      'assets/import_templates/modelo-planos.csv',
    );
    final modelBytes = modelData.buffer.asUint8List(
      modelData.offsetInBytes,
      modelData.lengthInBytes,
    );
    selectorMock.nextFile = XFile.fromData(
      modelBytes,
      path: 'modelo-planos.csv',
    );
    await tester.tap(find.text('Selecionar CSV/XLSX'));
    await tester.pumpAndSettle();
    expect(
      find.text('modelo-planos.csv'),
      findsOneWidget,
      reason: tester
          .widgetList<Text>(find.byType(Text))
          .map((text) => text.data)
          .join(' | '),
    );
    expect(find.textContaining('1 linhas.'), findsOneWidget);
    expect(selectorMock.openExtensions, ['csv', 'xlsx']);

    selectorMock.nextSaveLocation = null;
    await tester.ensureVisible(find.text('Baixar modelo'));
    await tester.tap(find.text('Baixar modelo'));
    await tester.pumpAndSettle();
    expect(selectorMock.suggestedName, 'modelo-planos.csv');
    expect(selectorMock.saveExtensions, ['csv']);
  });

  testWidgets('shows persisted final migration totals for a released batch', (
    tester,
  ) async {
    const sessionId = '00000000-0000-0000-0000-000000000131';
    final api = ApiClient(
      baseUrl: 'https://example.invalid',
      client: MockClient((request) async {
        if (request.url.path == '/api/migrations') {
          return http.Response(
            jsonEncode([
              {
                'sessionId': sessionId,
                'name': 'Totais da sessão',
                'sourceNamespace': 'synthetic',
                'status': 3,
                'createdAt': '2026-10-19T12:00:00Z',
                'expiresAt': '2099-10-19T12:00:00Z',
                'batchCount': 1,
              },
            ]),
            200,
          );
        }
        if (request.url.path == '/api/migrations/$sessionId') {
          return http.Response(
            jsonEncode({
              'sessionId': sessionId,
              'name': 'Totais da sessão',
              'sourceNamespace': 'synthetic',
              'status': 3,
              'expiresAt': '2099-10-19T12:00:00Z',
              'batches': [
                {
                  'batchId': '00000000-0000-0000-0000-000000000132',
                  'entityType': 1,
                  'rowCount': 4,
                  'errorCount': 0,
                  'ambiguousCount': 0,
                  'status': 'Released',
                  'totals': {
                    'successCount': 2,
                    'ignoredCount': 2,
                    'errorCount': 0,
                    'reviewedCount': 3,
                  },
                },
              ],
              'canRelease': false,
              'releaseBlockers': [],
              'results': [
                for (final (rowNumber, status) in [
                  (2, 'Imported'),
                  (3, 'Imported'),
                  (4, 'Ignored'),
                  (5, 'Ignored'),
                ])
                  {
                    'entityType': 1,
                    'rowNumber': rowNumber,
                    'status': status,
                    'message': 'Resultado preservado',
                  },
              ],
            }),
            200,
          );
        }
        return http.Response('Not found', 404);
      }),
    );

    await tester.pumpWidget(
      Provider<ApiClient>.value(
        value: api,
        child: const MaterialApp(home: MigrationWizardPage()),
      ),
    );
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Abrir'));
    await tester.tap(find.text('Abrir'));
    await tester.pumpAndSettle();

    expect(
      find.text('Lote 1 · 4 registros · 0 erros · Liberado'),
      findsOneWidget,
    );
    expect(
      find.text(
        'Totais finais · Sucesso 2 · Ignorados 2 · Erros 0 · Revisados 3',
      ),
      findsOneWidget,
    );
    expect(find.textContaining('Resultado preservado'), findsNWidgets(4));
  });

  testWidgets(
    'offers review for a confirmed plan blocking operational release',
    (tester) async {
      const sessionId = '00000000-0000-0000-0000-000000000121';
      const batchId = '00000000-0000-0000-0000-000000000122';
      var reviewOpened = false;
      final api = ApiClient(
        baseUrl: 'https://example.invalid',
        client: MockClient((request) async {
          if (request.url.path == '/api/migrations') {
            return http.Response(
              jsonEncode([
                {
                  'sessionId': sessionId,
                  'name': 'Conflict session',
                  'sourceNamespace': 'legacy',
                  'status': 1,
                  'createdAt': '2026-10-19T12:00:00Z',
                  'expiresAt': '2099-10-19T12:00:00Z',
                  'batchCount': 1,
                },
              ]),
              200,
            );
          }
          if (request.url.path == '/api/migrations/$sessionId') {
            return http.Response(
              jsonEncode({
                'sessionId': sessionId,
                'name': 'Conflict session',
                'sourceNamespace': 'legacy',
                'status': 1,
                'expiresAt': '2099-10-19T12:00:00Z',
                'batches': [
                  {
                    'batchId': batchId,
                    'entityType': 2,
                    'rowCount': 1,
                    'errorCount': 0,
                    'ambiguousCount': 1,
                    'status': 'Confirmed',
                  },
                ],
                'canRelease': false,
                'releaseBlockers': ['Plano em conflito (lote $batchId).'],
                'results': [],
              }),
              200,
            );
          }
          if (request.url.path ==
              '/api/migrations/$sessionId/batches/$batchId/review') {
            reviewOpened = true;
            return http.Response(
              jsonEncode({
                'batchId': batchId,
                'entityType': 2,
                'rows': [
                  {
                    'rowNumber': 2,
                    'isValid': true,
                    'errors': [],
                    'candidates': [
                      {
                        'targetId': '00000000-0000-0000-0000-000000000123',
                        'sourceRowNumber': null,
                        'name': 'Plano existente',
                        'description': 'Nome existente · 80,00 · 3 meses',
                        'isArchived': false,
                      },
                    ],
                    'values': {
                      'externalId': 'plan-1',
                      'name': 'Plano existente',
                    },
                    'requiresFinancialResolution': false,
                  },
                ],
                'alreadyStaged': true,
                'decisions': [
                  {'rowNumber': 2, 'action': 'ImportNew', 'targetId': null},
                ],
                'fingerprint': 'prepared-plan-fingerprint',
              }),
              200,
            );
          }
          return http.Response('Not found', 404);
        }),
      );

      await tester.pumpWidget(
        Provider<ApiClient>.value(
          value: api,
          child: const MaterialApp(home: MigrationWizardPage()),
        ),
      );
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Retomar'));
      await tester.tap(find.text('Retomar'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Resolver conflito'));
      await tester.tap(find.text('Resolver conflito'));
      await tester.pumpAndSettle();

      expect(reviewOpened, isTrue);
      expect(find.text('Confirmar revisão do lote'), findsOneWidget);
      expect(find.textContaining('Plano existente'), findsWidgets);
    },
  );
}

class _TestFileSelector extends FileSelectorPlatform {
  XFile? nextFile;
  FileSaveLocation? nextSaveLocation;
  List<String>? openExtensions;
  List<String>? saveExtensions;
  String? suggestedName;

  @override
  Future<XFile?> openFile({
    List<XTypeGroup>? acceptedTypeGroups,
    String? initialDirectory,
    String? confirmButtonText,
  }) async {
    openExtensions = acceptedTypeGroups?.single.extensions;
    return nextFile;
  }

  @override
  Future<FileSaveLocation?> getSaveLocation({
    List<XTypeGroup>? acceptedTypeGroups,
    SaveDialogOptions options = const SaveDialogOptions(),
  }) async {
    saveExtensions = acceptedTypeGroups?.single.extensions;
    suggestedName = options.suggestedName;
    return nextSaveLocation;
  }
}
