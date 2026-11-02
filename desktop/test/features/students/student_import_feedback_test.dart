import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:avelri_gestao/app/app.dart';
import 'package:avelri_gestao/app/router/app_router.dart';
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
import 'package:avelri_gestao/features/students/data/student_import_api_service.dart';
import 'package:avelri_gestao/features/students/presentation/student_import_view_model.dart';
import 'package:file_selector_platform_interface/file_selector_platform_interface.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import '../../support/test_session_storage.dart';

const _batchId = '00000000-0000-0000-0000-000000000130';

Map<String, dynamic> _previewJson({
  bool alreadyCompleted = false,
  List<Map<String, dynamic>>? results,
}) => {
  'batchId': _batchId,
  'alreadyCompleted': alreadyCompleted,
  'rows': alreadyCompleted
      ? <Object>[]
      : [
          {
            'rowNumber': 2,
            'isValid': true,
            'errors': <String>[],
            'candidates': <Object>[],
          },
          {
            'rowNumber': 3,
            'isValid': true,
            'errors': <String>[],
            'candidates': <Object>[],
          },
        ],
  'results': results ?? <Object>[],
};

Map<String, dynamic> _previewWithPossibleDuplicateJson() => {
  'batchId': _batchId,
  'alreadyCompleted': false,
  'rows': [
    {
      'rowNumber': 2,
      'isValid': true,
      'errors': <String>[],
      'candidates': [
        {
          'name': 'Aluna existente sintética',
          'matchBy': 'Email',
          'studentId': 'synthetic-student-id',
          'email': 'existing@example.invalid',
          'phone': null,
          'isArchived': false,
        },
      ],
    },
    {
      'rowNumber': 3,
      'isValid': true,
      'errors': <String>[],
      'candidates': <Object>[],
    },
  ],
  'results': <Object>[],
};

Map<String, dynamic> _resultRow(int rowNumber, String status) => {
  'rowNumber': rowNumber,
  'status': status,
  'message': 'Resultado sintético.',
};

http.Response _studentsResponse() => http.Response(
  jsonEncode({
    'items': <Object>[],
    'page': 1,
    'pageSize': 20,
    'totalCount': 0,
    'totalPages': 0,
  }),
  200,
);

void main() {
  late FlutterSecureStoragePlatform originalStoragePlatform;
  late FileSelectorPlatform originalFileSelectorPlatform;
  late _TestFileSelector fileSelector;

  setUp(() {
    originalStoragePlatform = FlutterSecureStoragePlatform.instance;
    FlutterSecureStoragePlatform.instance = TestFlutterSecureStoragePlatform(
      {},
    );
    originalFileSelectorPlatform = FileSelectorPlatform.instance;
    fileSelector = _TestFileSelector()
      ..file = XFile.fromData(
        Uint8List.fromList(
          utf8.encode('Nome\nAluna sintética A\nAluna sintética B\n'),
        ),
        path: 'alunos-sinteticos.csv',
      );
    FileSelectorPlatform.instance = fileSelector;
  });

  tearDown(() {
    FlutterSecureStoragePlatform.instance = originalStoragePlatform;
    FileSelectorPlatform.instance = originalFileSelectorPlatform;
  });

  testWidgets(
    'possible duplicate starts unselected and blocks confirmation until explicit choice',
    (tester) async {
      var confirmRequests = 0;
      final fixture = await _mount(
        tester,
        onRequest: (request) async {
          if (request.url.path == '/api/gym-features') {
            return http.Response('{"erpEnabled":true}', 200);
          }
          if (request.url.path == '/api/students') return _studentsResponse();
          if (request.url.path == '/api/students/imports/preview') {
            return http.Response(
              jsonEncode(_previewWithPossibleDuplicateJson()),
              200,
            );
          }
          if (request.url.path.endsWith('/confirm')) confirmRequests++;
          return http.Response('Not found', 404);
        },
      );
      addTearDown(fixture.dispose);

      await _openPreview(tester);

      expect(find.textContaining('Possível duplicidade:'), findsOneWidget);
      expect(find.textContaining('Aluna existente sintética'), findsOneWidget);
      final decision = tester.widget<DropdownButton<String>>(
        find.byKey(const ValueKey('student-import-decision-2')),
      );
      expect(decision.value, isNull);
      expect(find.text('Escolha uma decisão'), findsOneWidget);

      await tester.ensureVisible(find.text('Criar como ativo'));
      await tester.tap(find.text('Criar como ativo'));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const ValueKey('student-import-confirm')),
            )
            .onPressed,
        isNull,
      );

      await tester.ensureVisible(
        find.byKey(const ValueKey('student-import-decision-2')),
      );
      await tester.tap(find.byKey(const ValueKey('student-import-decision-2')));
      await tester.pumpAndSettle();
      expect(
        find.byKey(const ValueKey('student-import-decision-2-ImportAsNew')),
        findsOneWidget,
      );
      expect(
        find.byKey(const ValueKey('student-import-decision-2-Ignore')),
        findsOneWidget,
      );
      await tester.tap(
        find.byKey(const ValueKey('student-import-decision-2-Ignore')),
      );
      await tester.pumpAndSettle();

      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const ValueKey('student-import-confirm')),
            )
            .onPressed,
        isNotNull,
      );
      expect(confirmRequests, 0);
    },
  );

  testWidgets(
    'successful confirmation replaces the form with a visible result screen',
    (tester) async {
      var confirmRequests = 0;
      final confirmationResponse = Completer<http.Response>();
      final fixture = await _mount(
        tester,
        onRequest: (request) async {
          if (request.url.path == '/api/gym-features') {
            return http.Response('{"erpEnabled":true}', 200);
          }
          if (request.url.path == '/api/students') return _studentsResponse();
          if (request.url.path == '/api/students/imports/preview') {
            return http.Response(jsonEncode(_previewJson()), 200);
          }
          if (request.url.path.endsWith('/confirm')) {
            confirmRequests++;
            return confirmationResponse.future;
          }
          return http.Response('Not found', 404);
        },
      );
      addTearDown(fixture.dispose);

      await _openConfirmation(tester);
      await tester.tap(
        find.descendant(
          of: find.byType(AlertDialog),
          matching: find.text('Importar'),
        ),
      );
      await tester.pump();

      expect(confirmRequests, 1);
      expect(find.text('Importar alunos'), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const ValueKey('student-import-confirm')),
            )
            .onPressed,
        isNull,
      );
      await tester.tap(
        find.byKey(const ValueKey('student-import-confirm')),
        warnIfMissed: false,
      );
      await tester.pump();
      expect(confirmRequests, 1);
      confirmationResponse.complete(
        http.Response(
          jsonEncode({
            'batchId': _batchId,
            'alreadyCompleted': false,
            'results': [_resultRow(2, 'Imported'), _resultRow(3, 'Imported')],
          }),
          200,
        ),
      );
      await tester.pumpAndSettle();

      expect(
        find.byKey(const ValueKey('student-import-result-title')),
        findsOneWidget,
      );
      expect(find.text('Gestão de alunos'), findsNothing);
      expect(find.text('Importados: 2'), findsOneWidget);
      expect(find.text('Ignorados: 0'), findsOneWidget);
      expect(find.text('Erros: 0'), findsOneWidget);
      expect(
        find.textContaining('A ativação do acesso continua sendo'),
        findsOneWidget,
      );
      expect(find.text('Mapeamento de colunas'), findsNothing);
      expect(find.text('Revisar prévia'), findsNothing);
      expect(find.text('Selecionar CSV/XLSX'), findsNothing);
      expect(
        find.byKey(const ValueKey('student-import-view-students')),
        findsOneWidget,
      );
      expect(find.byKey(const ValueKey('student-import-new')), findsOneWidget);
      final resultRect = tester.getRect(
        find.byKey(const ValueKey('student-import-result-title')),
      );
      final viewportHeight =
          tester.view.physicalSize.height / tester.view.devicePixelRatio;
      expect(resultRect.top, greaterThanOrEqualTo(0));
      expect(resultRect.bottom, lessThanOrEqualTo(viewportHeight));
      expect(confirmRequests, 1);
    },
  );

  testWidgets('partial result shows backend totals and line details', (
    tester,
  ) async {
    final fixture = await _mount(
      tester,
      onRequest: (request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response('{"erpEnabled":true}', 200);
        }
        if (request.url.path == '/api/students') return _studentsResponse();
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_previewJson()), 200);
        }
        if (request.url.path.endsWith('/confirm')) {
          return http.Response(
            jsonEncode({
              'batchId': _batchId,
              'alreadyCompleted': false,
              'results': [_resultRow(2, 'Imported'), _resultRow(3, 'Error')],
            }),
            200,
          );
        }
        return http.Response('Not found', 404);
      },
    );
    addTearDown(fixture.dispose);

    await _completeAndShowResult(tester);

    expect(
      find.byKey(const ValueKey('student-import-result-title')),
      findsOneWidget,
    );
    expect(
      find.text(
        'A importação terminou parcialmente. Revise as linhas com erro.',
      ),
      findsOneWidget,
    );
    expect(find.text('Importados: 1'), findsOneWidget);
    expect(find.text('Ignorados: 0'), findsOneWidget);
    expect(find.text('Erros: 1'), findsOneWidget);
    expect(find.textContaining('Linha 3 · Aluna sintética B'), findsOneWidget);
    expect(find.text('Mapeamento de colunas'), findsNothing);
  });

  testWidgets('Ver alunos returns to the real list route and refreshes it', (
    tester,
  ) async {
    var confirmRequests = 0;
    var studentListReads = 0;
    final fixture = await _mount(
      tester,
      onRequest: (request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response('{"erpEnabled":true}', 200);
        }
        if (request.url.path == '/api/students') {
          studentListReads++;
          return _studentsResponse();
        }
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_previewJson()), 200);
        }
        if (request.url.path.endsWith('/confirm')) {
          confirmRequests++;
          return http.Response(
            jsonEncode({
              'batchId': _batchId,
              'alreadyCompleted': false,
              'results': [_resultRow(2, 'Imported'), _resultRow(3, 'Imported')],
            }),
            200,
          );
        }
        return http.Response('Not found', 404);
      },
    );
    addTearDown(fixture.dispose);

    await _completeAndShowResult(tester);

    expect(confirmRequests, 1);
    expect(studentListReads, 1);
    await tester.tap(
      find.byKey(const ValueKey('student-import-view-students')),
    );
    await tester.pumpAndSettle();

    expect(find.text('Gestão de alunos'), findsOneWidget);
    expect(
      find.byKey(const ValueKey('student-import-result-title')),
      findsNothing,
    );
    expect(studentListReads, 2);
    expect(confirmRequests, 1);
  });

  testWidgets(
    'complete response shows backend totals and processed line outcomes',
    (tester) async {
      final fixture = await _mount(
        tester,
        onRequest: (request) async {
          if (request.url.path == '/api/gym-features') {
            return http.Response('{"erpEnabled":true}', 200);
          }
          if (request.url.path == '/api/students') return _studentsResponse();
          if (request.url.path == '/api/students/imports/preview') {
            return http.Response(jsonEncode(_previewJson()), 200);
          }
          if (request.url.path.endsWith('/confirm')) {
            return http.Response(
              jsonEncode({
                'batchId': _batchId,
                'alreadyCompleted': false,
                'results': [
                  _resultRow(2, 'Imported'),
                  _resultRow(3, 'Ignored'),
                ],
              }),
              200,
            );
          }
          return http.Response('Not found', 404);
        },
      );
      addTearDown(fixture.dispose);

      await _completeAndShowResult(tester);

      expect(find.text('Importação concluída'), findsOneWidget);
      expect(
        find.text('Todos os registros foram processados sem erros.'),
        findsOneWidget,
      );
      expect(find.text('Importados: 1'), findsOneWidget);
      expect(find.text('Ignorados: 1'), findsOneWidget);
      expect(find.text('Erros: 0'), findsOneWidget);
      expect(
        find.textContaining('Nenhum aluno foi criado novamente'),
        findsNothing,
      );
    },
  );

  testWidgets(
    'already completed batch reports stored totals and no repeated creation',
    (tester) async {
      var confirmRequests = 0;
      final fixture = await _mount(
        tester,
        onRequest: (request) async {
          if (request.url.path == '/api/gym-features') {
            return http.Response('{"erpEnabled":true}', 200);
          }
          if (request.url.path == '/api/students') return _studentsResponse();
          if (request.url.path == '/api/students/imports/preview') {
            return http.Response(
              jsonEncode(
                _previewJson(
                  alreadyCompleted: true,
                  results: [
                    _resultRow(2, 'Imported'),
                    _resultRow(3, 'Ignored'),
                  ],
                ),
              ),
              200,
            );
          }
          if (request.url.path.endsWith('/confirm')) confirmRequests++;
          return http.Response('Not found', 404);
        },
      );
      addTearDown(fixture.dispose);

      await tester.tap(find.text('Importar CSV/XLSX'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Selecionar CSV/XLSX'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Gerar prévia'));
      await tester.tap(find.text('Gerar prévia'));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const ValueKey('student-import-result-title')),
        findsOneWidget,
      );
      expect(
        find.text(
          'Este lote já havia sido concluído. Nenhum aluno foi criado novamente.',
        ),
        findsOneWidget,
      );
      expect(
        find.text('Os totais abaixo são os resultados salvos do lote.'),
        findsOneWidget,
      );
      expect(find.text('Importados: 1'), findsOneWidget);
      expect(find.text('Ignorados: 1'), findsOneWidget);
      expect(find.text('Erros: 0'), findsOneWidget);
      expect(
        find.byKey(const ValueKey('student-import-confirm')),
        findsNothing,
      );
      expect(confirmRequests, 0);
    },
  );

  testWidgets('zero imported rows have a distinct result message', (
    tester,
  ) async {
    final fixture = await _mount(
      tester,
      onRequest: (request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response('{"erpEnabled":true}', 200);
        }
        if (request.url.path == '/api/students') return _studentsResponse();
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_previewJson()), 200);
        }
        if (request.url.path.endsWith('/confirm')) {
          return http.Response(
            jsonEncode({
              'batchId': _batchId,
              'alreadyCompleted': false,
              'results': [_resultRow(2, 'Ignored'), _resultRow(3, 'Error')],
            }),
            200,
          );
        }
        return http.Response('Not found', 404);
      },
    );
    addTearDown(fixture.dispose);

    await _completeAndShowResult(tester);

    expect(
      find.text(
        'Nenhum aluno novo foi importado. Revise os registros ignorados ou com erro.',
      ),
      findsOneWidget,
    );
    expect(find.text('Importados: 0'), findsOneWidget);
    expect(find.text('Ignorados: 1'), findsOneWidget);
    expect(find.text('Erros: 1'), findsOneWidget);
    expect(
      find.byKey(const ValueKey('student-import-result-title')),
      findsOneWidget,
    );
  });

  testWidgets('Nova importação clears the previous result and form state', (
    tester,
  ) async {
    final fixture = await _mount(
      tester,
      onRequest: (request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response('{"erpEnabled":true}', 200);
        }
        if (request.url.path == '/api/students') return _studentsResponse();
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_previewJson()), 200);
        }
        if (request.url.path.endsWith('/confirm')) {
          return http.Response(
            jsonEncode({
              'batchId': _batchId,
              'alreadyCompleted': false,
              'results': [_resultRow(2, 'Imported'), _resultRow(3, 'Imported')],
            }),
            200,
          );
        }
        return http.Response('Not found', 404);
      },
    );
    addTearDown(fixture.dispose);

    await _completeAndShowResult(tester);
    await tester.tap(find.byKey(const ValueKey('student-import-new')));
    await tester.pumpAndSettle();

    expect(
      find.byKey(const ValueKey('student-import-result-title')),
      findsNothing,
    );
    expect(find.text('Importar alunos'), findsOneWidget);
    expect(find.text('Selecionar CSV/XLSX'), findsOneWidget);
    expect(find.text('alunos-sinteticos.csv · 2 registros'), findsNothing);
    expect(find.text('Mapeamento de colunas'), findsNothing);
    expect(find.text('Revisar prévia'), findsNothing);

    await tester.tap(find.text('Selecionar CSV/XLSX'));
    await tester.pumpAndSettle();
    expect(find.text('alunos-sinteticos.csv · 2 registros'), findsOneWidget);
    expect(find.text('Revisar prévia'), findsNothing);
  });

  testWidgets('HTTP failure stays on the review form without a result screen', (
    tester,
  ) async {
    final fixture = await _mount(
      tester,
      onRequest: (request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response('{"erpEnabled":true}', 200);
        }
        if (request.url.path == '/api/students') return _studentsResponse();
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_previewJson()), 200);
        }
        if (request.url.path.endsWith('/confirm')) {
          return http.Response('{"message":"Falha sintética."}', 500);
        }
        return http.Response('Not found', 404);
      },
    );
    addTearDown(fixture.dispose);

    await _openConfirmation(tester);
    await tester.tap(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Importar'),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Falha sintética.'), findsOneWidget);
    expect(
      find.byKey(const ValueKey('student-import-result-title')),
      findsNothing,
    );
    expect(find.text('Importar alunos'), findsOneWidget);
    expect(find.text('Revisar prévia'), findsOneWidget);
    expect(find.text('Confirmar importação'), findsOneWidget);
  });

  testWidgets('confirmation timeout remains in review with no success screen', (
    tester,
  ) async {
    final fixture = await _mount(
      tester,
      requestTimeout: const Duration(milliseconds: 5),
      onRequest: (request) async {
        if (request.url.path == '/api/gym-features') {
          return http.Response('{"erpEnabled":true}', 200);
        }
        if (request.url.path == '/api/students') return _studentsResponse();
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_previewJson()), 200);
        }
        if (request.url.path.endsWith('/confirm')) {
          return Completer<http.Response>().future;
        }
        return http.Response('Not found', 404);
      },
    );
    addTearDown(fixture.dispose);

    await _openConfirmation(tester);
    await tester.tap(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Importar'),
      ),
    );
    await tester.pumpAndSettle();

    expect(
      find.byKey(const ValueKey('student-import-result-title')),
      findsNothing,
    );
    expect(find.text('Importar alunos'), findsOneWidget);
    expect(find.text('Revisar prévia'), findsOneWidget);
    expect(find.text('Confirmar importação'), findsOneWidget);
    expect(
      find.text('Não foi possível concluir a importação.'),
      findsOneWidget,
    );
  });

  test(
    'timeout and inconclusive confirmation responses do not create a result',
    () async {
      for (final mode in ['timeout', 'inconclusive']) {
        final api = ApiClient(
          baseUrl: 'https://synthetic.invalid/',
          requestTimeout: const Duration(milliseconds: 5),
          client: MockClient((request) async {
            if (request.url.path == '/api/students/imports/preview') {
              return http.Response(jsonEncode(_previewJson()), 200);
            }
            if (request.url.path.endsWith('/confirm')) {
              if (mode == 'timeout') return Completer<http.Response>().future;
              return http.Response('{"batchId":"$_batchId"}', 200);
            }
            return http.Response('Not found', 404);
          }),
        );
        final viewModel = StudentImportViewModel(StudentImportApiService(api));
        viewModel.loadFile(
          'synthetic.csv',
          Uint8List.fromList(
            utf8.encode('Nome\nAluna sintética A\nAluna sintética B\n'),
          ),
        );
        await viewModel.createPreview();
        viewModel.chooseNewStudentsStatus(true);
        await viewModel.confirmImport();

        expect(viewModel.result, isNull, reason: mode);
        expect(viewModel.errorMessage, isNotNull, reason: mode);
        viewModel.dispose();
        api.close();
      }
    },
  );
}

Future<void> _openConfirmation(WidgetTester tester) async {
  await _openPreview(tester);
  await tester.ensureVisible(find.text('Criar como ativo'));
  await tester.tap(find.text('Criar como ativo'));
  await tester.pump();
  await tester.ensureVisible(find.text('Confirmar importação'));
  final confirmButton = tester.widget<FilledButton>(
    find.byKey(const ValueKey('student-import-confirm')),
  );
  expect(confirmButton.onPressed, isNotNull);
  await tester.tap(find.byKey(const ValueKey('student-import-confirm')));
  await tester.pumpAndSettle();
  expect(find.byType(AlertDialog), findsOneWidget);
}

Future<void> _openPreview(WidgetTester tester) async {
  await tester.tap(find.text('Importar CSV/XLSX'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Selecionar CSV/XLSX'));
  await tester.pumpAndSettle();
  await tester.ensureVisible(find.text('Gerar prévia'));
  await tester.tap(find.text('Gerar prévia'));
  await tester.pumpAndSettle();
}

Future<void> _completeAndShowResult(WidgetTester tester) async {
  await _openConfirmation(tester);
  await tester.tap(
    find.descendant(
      of: find.byType(AlertDialog),
      matching: find.text('Importar'),
    ),
  );
  await tester.pumpAndSettle();
  expect(
    find.byKey(const ValueKey('student-import-result-title')),
    findsOneWidget,
  );
}

Future<_Fixture> _mount(
  WidgetTester tester, {
  required Future<http.Response> Function(http.Request request) onRequest,
  Duration requestTimeout = const Duration(seconds: 15),
}) async {
  tester.view.physicalSize = const Size(1600, 1200);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final api = ApiClient(
    baseUrl: 'https://synthetic.invalid/',
    requestTimeout: requestTimeout,
    client: MockClient(onRequest),
  );
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
      userId: 'synthetic-admin',
      gymId: 'synthetic-gym',
      name: 'Synthetic Admin',
      email: 'admin@example.invalid',
      role: AppRole.admin,
    ),
    token: 'synthetic-token',
  );
  final features = ErpFeatureController(api);
  await features.loadForGym('synthetic-gym');
  final branding = BrandingController(
    const LocalBrandingRepository({}),
    defaultBranding: avelriDefaultBranding,
  );
  await branding.loadForGym('synthetic-gym');
  final navigation = AppRouter(session, branding, features);
  final fixture = _Fixture(
    api,
    session,
    features,
    branding,
    navigation,
    tester,
  );
  await tester.pumpWidget(fixture.app);
  await tester.pumpAndSettle();
  navigation.router.go('/students');
  await tester.pumpAndSettle();
  return fixture;
}

class _Fixture {
  const _Fixture(
    this.api,
    this.session,
    this.features,
    this.branding,
    this.navigation,
    this.tester,
  );

  final ApiClient api;
  final SessionController session;
  final ErpFeatureController features;
  final BrandingController branding;
  final AppRouter navigation;
  final WidgetTester tester;

  Widget get app => AvelriGestaoApp(
    sessionController: session,
    brandingController: branding,
    erpFeatureController: features,
    apiClient: api,
    router: navigation.router,
  );

  Future<void> dispose() async {
    await tester.pumpWidget(const SizedBox.shrink());
    navigation.dispose();
    session.dispose();
    features.dispose();
    branding.dispose();
    api.close();
  }
}

class _TestFileSelector extends FileSelectorPlatform {
  XFile? file;

  @override
  Future<XFile?> openFile({
    List<XTypeGroup>? acceptedTypeGroups,
    String? initialDirectory,
    String? confirmButtonText,
  }) async => file;
}
