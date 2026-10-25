import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/presentation/migration_wizard_page.dart';
import 'package:avelri_gestao/features/students/presentation/student_import_page.dart';
import 'package:file_selector_platform_interface/file_selector_platform_interface.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

void main() {
  testWidgets('back returns to the previous page for both migration flows', (
    tester,
  ) async {
    final api = _api();
    addTearDown(api.close);
    final router = _router(initialLocation: '/students');
    addTearDown(router.dispose);
    await tester.pumpWidget(_app(api, router));
    await tester.enterText(
      find.byKey(const ValueKey('students-search')),
      'Ana',
    );

    router.push('/students/import');
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('student-import-back')));
    await tester.pumpAndSettle();
    expect(router.routeInformationProvider.value.uri.path, '/students');
    expect(find.text('Ana'), findsOneWidget);

    router.push('/students/migration');
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('migration-wizard-back')));
    await tester.pumpAndSettle();
    expect(router.routeInformationProvider.value.uri.path, '/students');
    expect(find.text('Ana'), findsOneWidget);
  });

  testWidgets('direct migration routes fall back to the students list', (
    tester,
  ) async {
    final api = _api();
    addTearDown(api.close);
    for (final path in ['/students/import', '/students/migration']) {
      final router = _router(initialLocation: path);
      await tester.pumpWidget(_app(api, router));
      await tester.pumpAndSettle();
      final back = find.byKey(
        ValueKey(
          path.endsWith('/import')
              ? 'student-import-back'
              : 'migration-wizard-back',
        ),
      );
      await tester.tap(back);
      await tester.pumpAndSettle();
      expect(router.routeInformationProvider.value.uri.path, '/students');
      expect(find.text('Lista de alunos'), findsOneWidget);
      await tester.pumpWidget(const SizedBox.shrink());
      router.dispose();
    }
  });

  testWidgets('back buttons are reachable and activatable by keyboard', (
    tester,
  ) async {
    final api = _api();
    addTearDown(api.close);
    final router = _router(initialLocation: '/students/import');
    addTearDown(router.dispose);
    await tester.pumpWidget(_app(api, router));

    await tester.sendKeyEvent(LogicalKeyboardKey.tab);
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pumpAndSettle();
    expect(router.routeInformationProvider.value.uri.path, '/students');

    router.go('/students/migration');
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.tab);
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pumpAndSettle();
    expect(router.routeInformationProvider.value.uri.path, '/students');
  });

  testWidgets('import back confirms draft loss and blocks during preview', (
    tester,
  ) async {
    final originalSelector = FileSelectorPlatform.instance;
    final selector = _TestFileSelector()
      ..nextFile = XFile.fromData(
        Uint8List.fromList(utf8.encode('Nome,E-mail\nAna,ana@example.invalid')),
        path: 'alunos.csv',
      );
    FileSelectorPlatform.instance = selector;
    addTearDown(() => FileSelectorPlatform.instance = originalSelector);

    final previewResponse = Completer<http.Response>();
    final api = _api(
      onRequest: (request) {
        if (request.url.path == '/api/students/imports/preview') {
          return previewResponse.future;
        }
        return Future.value(http.Response('Not found', 404));
      },
    );
    addTearDown(api.close);
    final router = _router(initialLocation: '/students/import');
    addTearDown(router.dispose);
    await tester.pumpWidget(_app(api, router));
    await tester.tap(find.text('Selecionar CSV/XLSX'));
    await tester.pumpAndSettle();
    expect(find.textContaining('alunos.csv'), findsOneWidget);

    await tester.ensureVisible(find.text('Gerar prévia'));
    await tester.tap(find.text('Gerar prévia'));
    await tester.pump();
    await tester.ensureVisible(
      find.byKey(const ValueKey('student-import-back')),
    );
    await tester.tap(find.byKey(const ValueKey('student-import-back')));
    await tester.pump();
    expect(router.routeInformationProvider.value.uri.path, '/students/import');
    expect(
      find.text('Aguarde a operação de importação terminar antes de sair.'),
      findsOneWidget,
    );

    previewResponse.complete(
      http.Response(
        jsonEncode({
          'batchId': 'synthetic-batch',
          'alreadyCompleted': false,
          'rows': [
            {
              'rowNumber': 2,
              'isValid': true,
              'errors': <String>[],
              'candidates': <Object>[],
            },
          ],
          'results': <Object>[],
        }),
        200,
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('student-import-back')));
    await tester.pumpAndSettle();
    expect(find.text('Sair da importação?'), findsOneWidget);
    await tester.tap(find.text('Continuar importando'));
    await tester.pumpAndSettle();
    expect(find.textContaining('alunos.csv'), findsOneWidget);
    expect(router.routeInformationProvider.value.uri.path, '/students/import');

    await tester.tap(find.byKey(const ValueKey('student-import-back')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sair e descartar'));
    await tester.pumpAndSettle();
    expect(router.routeInformationProvider.value.uri.path, '/students');
  });

  testWidgets('migration back confirms unsaved session details', (
    tester,
  ) async {
    final api = _api();
    addTearDown(api.close);
    final router = _router(initialLocation: '/students/migration');
    addTearDown(router.dispose);
    await tester.pumpWidget(_app(api, router));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).at(0), 'Migração teste');
    await tester.enterText(find.byType(TextField).at(1), 'Sistema legado');
    await tester.tap(find.byKey(const ValueKey('migration-wizard-back')));
    await tester.pumpAndSettle();
    expect(find.text('Sair da migração?'), findsOneWidget);
    await tester.tap(find.text('Continuar na migração'));
    await tester.pumpAndSettle();
    expect(find.text('Migração teste'), findsOneWidget);
    expect(
      router.routeInformationProvider.value.uri.path,
      '/students/migration',
    );

    await tester.tap(find.byKey(const ValueKey('migration-wizard-back')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sair e descartar'));
    await tester.pumpAndSettle();
    expect(router.routeInformationProvider.value.uri.path, '/students');
  });

  testWidgets('migration back does not interrupt an active request', (
    tester,
  ) async {
    final sessionsResponse = Completer<http.Response>();
    final api = _api(
      onRequest: (request) {
        if (request.url.path == '/api/migrations') {
          return sessionsResponse.future;
        }
        return Future.value(http.Response('Not found', 404));
      },
    );
    addTearDown(api.close);
    final router = _router(initialLocation: '/students/migration');
    addTearDown(router.dispose);
    await tester.pumpWidget(_app(api, router));
    await tester.pump();

    await tester.tap(find.byKey(const ValueKey('migration-wizard-back')));
    await tester.pump();
    expect(
      router.routeInformationProvider.value.uri.path,
      '/students/migration',
    );
    expect(
      find.text('Aguarde a operação de migração terminar antes de sair.'),
      findsOneWidget,
    );

    sessionsResponse.complete(http.Response('[]', 200));
    await tester.pumpAndSettle();
    expect(
      router.routeInformationProvider.value.uri.path,
      '/students/migration',
    );
  });
}

ApiClient _api({
  Future<http.Response> Function(http.Request request)? onRequest,
}) => ApiClient(
  baseUrl: 'https://synthetic.invalid/',
  client: MockClient(onRequest ?? (_) async => http.Response('[]', 200)),
);

Widget _app(ApiClient api, GoRouter router) => Provider<ApiClient>.value(
  value: api,
  child: MaterialApp.router(routerConfig: router),
);

GoRouter _router({required String initialLocation}) => GoRouter(
  initialLocation: initialLocation,
  routes: [
    GoRoute(
      path: '/students',
      builder: (context, state) => const Scaffold(
        body: Column(
          children: [
            TextField(key: ValueKey('students-search')),
            Text('Lista de alunos'),
          ],
        ),
      ),
    ),
    GoRoute(
      path: '/students/import',
      builder: (context, state) => const Scaffold(body: StudentImportPage()),
    ),
    GoRoute(
      path: '/students/migration',
      builder: (context, state) => const Scaffold(body: MigrationWizardPage()),
    ),
  ],
);

class _TestFileSelector extends FileSelectorPlatform {
  XFile? nextFile;

  @override
  Future<XFile?> openFile({
    List<XTypeGroup>? acceptedTypeGroups,
    String? initialDirectory,
    String? confirmButtonText,
  }) async => nextFile;
}
