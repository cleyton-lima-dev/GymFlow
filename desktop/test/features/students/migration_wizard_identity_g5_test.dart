import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/presentation/migration_wizard_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

const _sessionId = '00000000-0000-0000-0000-000000000121';
const _batchId = '00000000-0000-0000-0000-000000000122';
const _studentId = '00000000-0000-0000-0000-000000000123';

void main() {
  testWidgets(
    'explicitly selects a student without contacts using paged search',
    (tester) async {
      final searches = <http.Request>[];
      Map<String, dynamic>? saved;
      final api = _createApi(
        onSearch: (request) => searches.add(request),
        onSaved: (body) => saved = body,
      );
      api.setAccessToken('synthetic-admin-token');
      addTearDown(api.close);
      await _openReview(tester, api);

      await tester.tap(
        find.widgetWithText(DropdownButton<String>, 'Criar novo'),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Vincular existente').last);
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Buscar aluno existente'));
      await tester.tap(find.text('Buscar aluno existente'));
      await tester.pumpAndSettle();

      expect(searches.single.url.queryParameters['archiveFilter'], '3');
      expect(
        searches.single.url.queryParameters.containsKey('isActive'),
        isFalse,
      );
      expect(searches.single.url.queryParameters['search'], 'Ana sem contato');
      expect(
        searches.single.headers['Authorization'],
        'Bearer synthetic-admin-token',
      );
      expect(saved, isNull);

      await tester.tap(find.text('Próxima'));
      await tester.pumpAndSettle();
      expect(searches.last.url.queryParameters['page'], '2');
      expect(find.textContaining('Sem conta digital'), findsOneWidget);
      await tester.tap(
        find.descendant(
          of: find.byType(AlertDialog),
          matching: find.widgetWithText(ListTile, 'Ana sem contato'),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byType(AlertDialog), findsNothing);
      expect(find.textContaining(_studentId), findsOneWidget);

      await tester.ensureVisible(find.text('Salvar decisões'));
      await tester.tap(find.text('Salvar decisões'));
      await tester.pumpAndSettle();
      final decision = (saved!['decisions'] as List).single as Map;
      expect(decision['action'], 'LinkExisting');
      expect(decision['targetId'], _studentId);
      expect(saved!['fingerprint'], 'student-g5-fingerprint');
    },
  );

  testWidgets('deduplicates candidate targets when reopening a known alias', (
    tester,
  ) async {
    final api = _createApi(
      confirmed: true,
      candidates: [
        _candidate('Vínculo existente'),
        _candidate('E-mail e telefone'),
      ],
      decisions: [
        {'rowNumber': 2, 'action': 'LinkExisting', 'targetId': _studentId},
      ],
    );
    addTearDown(api.close);
    await _openReview(tester, api, confirmed: true);

    expect(tester.takeException(), isNull);
    final selector = _targetSelector(tester);
    expect(selector.value, _studentId);
    expect(selector.items, hasLength(1));
    expect(selector.items!.single.value, _studentId);
  });

  testWidgets('preserves a manually selected target absent from candidates', (
    tester,
  ) async {
    Map<String, dynamic>? saved;
    final api = _createApi(
      decisions: [
        {'rowNumber': 2, 'action': 'LinkExisting', 'targetId': _studentId},
      ],
      onSaved: (body) => saved = body,
    );
    addTearDown(api.close);
    await _openReview(tester, api);

    expect(tester.takeException(), isNull);
    expect(_targetSelector(tester).value, _studentId);
    expect(find.text('Aluno selecionado · $_studentId'), findsOneWidget);
    expect(find.textContaining('studentIdentityContext'), findsNothing);
    await tester.ensureVisible(find.text('Salvar decisões'));
    await tester.tap(find.text('Salvar decisões'));
    await tester.pumpAndSettle();
    expect(
      ((saved!['decisions'] as List).single as Map)['targetId'],
      _studentId,
    );
  });
}

DropdownButton<String> _targetSelector(WidgetTester tester) => tester
    .widgetList<DropdownButton<String>>(find.byType(DropdownButton<String>))
    .singleWhere(
      (widget) => (widget.hint as Text?)?.data == 'Selecione destino',
    );

Map<String, dynamic> _candidate(String description) => {
  'targetId': _studentId,
  'sourceRowNumber': null,
  'name': 'Ana sem contato',
  'description': description,
  'isArchived': false,
};

ApiClient _createApi({
  List<Map<String, dynamic>> candidates = const [],
  List<Map<String, dynamic>> decisions = const [],
  bool confirmed = false,
  void Function(http.Request)? onSearch,
  void Function(Map<String, dynamic>)? onSaved,
}) => ApiClient(
  baseUrl: 'https://example.invalid',
  client: MockClient((request) async {
    Object? response;
    if (request.url.path == '/api/migrations') {
      response = [
        {
          'sessionId': _sessionId,
          'name': 'Identidade G5',
          'sourceNamespace': 'legacy',
          'status': 1,
          'createdAt': '2026-10-19T12:00:00Z',
          'expiresAt': '2099-10-19T12:00:00Z',
          'batchCount': 1,
        },
      ];
    } else if (request.url.path == '/api/migrations/$_sessionId') {
      response = {
        'sessionId': _sessionId,
        'name': 'Identidade G5',
        'sourceNamespace': 'legacy',
        'status': 1,
        'expiresAt': '2099-10-19T12:00:00Z',
        'batches': [
          {
            'batchId': _batchId,
            'entityType': 1,
            'rowCount': 1,
            'errorCount': 0,
            'ambiguousCount': candidates.length,
            'status': confirmed ? 'Confirmed' : 'Previewed',
          },
        ],
        'canRelease': false,
        'releaseBlockers': confirmed
            ? ['Aluno alterado (lote $_batchId).']
            : [],
        'results': [],
      };
    } else if (request.url.path ==
        '/api/migrations/$_sessionId/batches/$_batchId/review') {
      if (request.method == 'POST') {
        onSaved?.call(
          Map<String, dynamic>.from(jsonDecode(request.body) as Map),
        );
        return http.Response('', 204);
      }
      response = {
        'batchId': _batchId,
        'entityType': 1,
        'rows': [
          {
            'rowNumber': 2,
            'isValid': true,
            'errors': [],
            'candidates': candidates,
            'values': {
              'externalId': 'legacy-ana',
              'name': 'Ana sem contato',
              'studentIdentityContext': 'internal-context-hash',
            },
            'requiresFinancialResolution': false,
          },
        ],
        'alreadyStaged': true,
        'decisions': decisions,
        'fingerprint': 'student-g5-fingerprint',
      };
    } else if (request.url.path == '/api/students') {
      onSearch?.call(request);
      response = {
        'items': request.url.queryParameters['page'] == '2'
            ? [
                {
                  'id': _studentId,
                  'name': 'Ana sem contato',
                  'email': null,
                  'phone': null,
                  'birthDate': '1980-01-02',
                  'isActive': false,
                  'archivedAt': null,
                  'userId': null,
                },
              ]
            : [],
        'page': int.parse(request.url.queryParameters['page']!),
        'pageSize': 20,
        'totalCount': 21,
        'totalPages': 2,
      };
    } else {
      return http.Response('Not found', 404);
    }
    return http.Response(jsonEncode(response), 200);
  }),
);

Future<void> _openReview(
  WidgetTester tester,
  ApiClient api, {
  bool confirmed = false,
}) async {
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
  final label = confirmed ? 'Resolver conflito' : 'Retomar revisão';
  await tester.ensureVisible(find.text(label));
  await tester.tap(find.text(label));
  await tester.pumpAndSettle();
  await tester.ensureVisible(find.byType(ExpansionTile));
  await tester.tap(find.byType(ExpansionTile));
  await tester.pumpAndSettle();
}
