import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';

import 'dart:convert';

import 'package:avelri_platform/platform_app.dart';
import 'package:avelri_platform/platform_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

Map<String, dynamic> academy(int id, {bool completed = false}) => {
  'gymId': 'synthetic-$id',
  'administrativeName': 'Academia $id',
  'adminName': 'Admin $id',
  'adminEmail': 'admin$id@example.test',
  'timeZoneId': 'America/Sao_Paulo',
  'status': completed ? 'completed' : 'awaiting-admin',
  'adminActivatedAt': completed ? '2026-10-12T12:00:00Z' : null,
  'completedAt': completed ? '2026-10-12T12:30:00Z' : null,
  'configurationConfirmed': completed,
  'physicalAccessContracted': false,
  'physicalValidationEvidence': null,
  'version': 1,
};
http.Response response(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);
Future<void> login(WidgetTester tester) async {
  await tester.enterText(
    find.byType(TextFormField).at(0),
    'operator@example.test',
  );
  await tester.enterText(
    find.byType(TextFormField).at(1),
    'synthetic-password',
  );
  await tester.enterText(find.byType(TextFormField).at(2), '123456');
  await tester.ensureVisible(find.text('Entrar com MFA'));
  await tester.tap(find.text('Entrar com MFA'));
  await tester.pumpAndSettle();
}

Future<void> navigate(WidgetTester tester, int section) async {
  final menu = find.byTooltip('Abrir menu');
  if (menu.evaluate().isNotEmpty) {
    await tester.tap(menu);
    await tester.pumpAndSettle();
  }
  await tester.tap(find.byKey(ValueKey('nav-$section')));
  await tester.pumpAndSettle();
}

Future<void> capture(WidgetTester tester, String name) async {
  if (!const bool.fromEnvironment('CAPTURE_LAYOUTS')) return;
  await tester.pumpAndSettle();
  final boundary = tester.renderObject<RenderRepaintBoundary>(
    find.byKey(const ValueKey('preview')),
  );
  await tester.runAsync(() async {
    final image = await boundary.toImage();
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    Directory('build/layout-review').createSync(recursive: true);
    File('build/layout-review/$name.png')
        .writeAsBytesSync(bytes!.buffer.asUint8List());
    image.dispose();
  });
}

void main() {
  setUpAll(() async {
    if (const bool.fromEnvironment('CAPTURE_LAYOUTS')) {
      final path = Platform.environment['PLATFORM_PREVIEW_FONT'];
      if (path != null) {
        final font = FontLoader('Roboto')
          ..addFont(
            Future.value(ByteData.sublistView(File(path).readAsBytesSync())),
          );
        await font.load();
        final icons = FontLoader('MaterialIcons')
          ..addFont(
            Future.value(
              ByteData.sublistView(
                File('${File(path).parent.path}/materialicons-regular.otf')
                    .readAsBytesSync(),
              ),
            ),
          );
        await icons.load();
      }
    }
  });
  for (final size in [
    const Size(360, 800),
    const Size(800, 600),
    const Size(1366, 768),
    const Size(1920, 1080),
  ]) {
    testWidgets('responsive shell, filters, onboarding and form at $size', (
      tester,
    ) async {
      final previousHandler = FlutterError.onError;
      FlutterError.onError = (details) {
        debugPrint(details.toString());
        previousHandler?.call(details);
      };
      addTearDown(() => FlutterError.onError = previousHandler);
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          if (r.url.path.endsWith('/auth/login')) {
            return response({
              'token': 'synthetic',
              'name': 'Operador sintético',
            });
          }
          if (r.url.path.endsWith('/synthetic-1')) {
            return response({
              'academy': academy(1),
              'audit': [
                {
                  'action': 'step-configuration',
                  'occurredAt': '2026-10-12T13:00:00Z',
                  'actorType': 'operator',
                },
                {
                  'action': 'provisioned',
                  'occurredAt': '2026-10-12T12:00:00Z',
                  'actorType': 'operator',
                },
              ],
            });
          }
          return response({
            'items': [academy(1), academy(2, completed: true)],
            'total': 2,
          });
        }),
      );
      await tester.pumpWidget(
        RepaintBoundary(
          key: const ValueKey('preview'),
          child: PlatformApp(api: api),
        ),
      );
      await capture(tester, 'login-${size.width.toInt()}');
      await login(tester);
      await capture(tester, 'overview-${size.width.toInt()}');
      expect(
        tester.widget<Text>(find.byKey(const ValueKey('metric-0'))).data,
        '2',
      );
      expect(
        tester.widget<Text>(find.byKey(const ValueKey('metric-1'))).data,
        '1',
      );
      expect(
        tester.widget<Text>(find.byKey(const ValueKey('metric-2'))).data,
        '1',
      );
      expect(tester.takeException(), isNull);
      await navigate(tester, 1);
      await tester.ensureVisible(find.byKey(const ValueKey('academy-search')));
      await tester.enterText(
        find.byKey(const ValueKey('academy-search')),
        'Academia 1',
      );
      await tester.pumpAndSettle();
      expect(find.text('Academia 1', findRichText: true).last, findsOneWidget);
      expect(find.text('Academia 2'), findsNothing);
      await navigate(tester, 2);
      expect(
        tester
            .widget<TextField>(find.byKey(const ValueKey('academy-search')))
            .controller!
            .text,
        'Academia 1',
      );
      expect(find.text('0 de 3 etapas concluídas'), findsOneWidget);
      await tester.ensureVisible(
        find.text('Academia 1', findRichText: true).last,
      );
      await tester.tap(find.text('Academia 1', findRichText: true).last);
      await tester.pumpAndSettle();
      await capture(tester, 'onboarding-${size.width.toInt()}');
      expect(find.text('Ativação do primeiro Admin'), findsOneWidget);
      expect(find.text('Homologação física'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('Histórico'));
      await tester.pumpAndSettle();
      expect(
        tester.getTopLeft(find.text('Academia provisionada')).dy,
        lessThan(tester.getTopLeft(find.text('Dados iniciais confirmados')).dy),
      );
      await tester.tap(find.text('Fechar'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Nova academia'));
      await tester.tap(find.text('Nova academia'));
      await tester.pumpAndSettle();
      await capture(tester, 'provision-${size.width.toInt()}');
      await tester.enterText(
        find.byType(TextFormField).first,
        'Nome preservado',
      );
      tester.view.physicalSize = size.width < 1000
          ? const Size(1366, 768)
          : const Size(360, 800);
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextFormField>(find.byType(TextFormField).first)
            .controller!
            .text,
        'Nome preservado',
      );
      expect(tester.takeException(), isNull);
      await tester.tap(find.text('Fechar'));
      await tester.pumpAndSettle();
      await tester.pumpWidget(const SizedBox());
    });
  }
  testWidgets(
    'overview aggregates every API page and searches beyond first page',
    (tester) async {
      final pages = <String>[];
      final all = List.generate(101, (i) => academy(i, completed: i >= 75));
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          if (r.url.path.endsWith('/auth/login')) {
            return response({'token': 'synthetic', 'name': 'Operador'});
          }
          final page = r.url.queryParameters['page']!;
          pages.add(page);
          expect(r.url.queryParameters['pageSize'], '100');
          return response({
            'total': 101,
            'items': page == '1'
                ? all.take(100).toList()
                : all.skip(100).toList(),
          });
        }),
      );
      await tester.pumpWidget(
        RepaintBoundary(
          key: const ValueKey('preview'),
          child: PlatformApp(api: api),
        ),
      );
      await login(tester);
      expect(pages, ['1', '2']);
      for (final entry in {0: '101', 1: '75', 2: '26'}.entries) {
        expect(
          tester.widget<Text>(find.byKey(ValueKey('metric-${entry.key}'))).data,
          entry.value,
        );
      }
      await navigate(tester, 1);
      await tester.enterText(
        find.byKey(const ValueKey('academy-search')),
        'Academia 100',
      );
      await tester.pumpAndSettle();
      expect(find.text('Academia 100').last, findsOneWidget);
      expect(find.text('synthetic-100'), findsNothing);
    },
  );
  testWidgets(
    'failed second page never displays partial totals and retry recovers',
    (tester) async {
      var fail = true;
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          if (r.url.path.endsWith('/auth/login')) {
            return response({'token': 'synthetic', 'name': 'Operador'});
          }
          if (r.url.queryParameters['page'] == '2') {
            return fail
                ? http.Response('', 500)
                : response({
                    'items': [academy(1, completed: true)],
                    'total': 2,
                  });
          }
          return response({
            'items': [academy(0)],
            'total': 2,
          });
        }),
      );
      await tester.pumpWidget(
        RepaintBoundary(
          key: const ValueKey('preview'),
          child: PlatformApp(api: api),
        ),
      );
      await login(tester);
      expect(
        tester.widget<Text>(find.byKey(const ValueKey('metric-0'))).data,
        '—',
      );
      expect(find.text('Tentar novamente'), findsOneWidget);
      fail = false;
      await tester.ensureVisible(find.text('Tentar novamente'));
      await tester.tap(find.text('Tentar novamente'));
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const ValueKey('metric-0'))).data,
        '2',
      );
    },
  );
  testWidgets('login retains entered fields across wide and narrow layouts', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1366, 768);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      PlatformApp(
        api: PlatformApi(
          'https://example.invalid',
          client: MockClient((r) async => http.Response('', 500)),
        ),
      ),
    );
    await tester.enterText(
      find.byType(TextFormField).first,
      'synthetic@example.test',
    );
    await tester.enterText(
      find.byType(TextFormField).at(1),
      'synthetic-password',
    );
    tester.view.physicalSize = const Size(360, 800);
    await tester.pumpAndSettle();
    expect(
      tester
          .widget<TextFormField>(find.byType(TextFormField).first)
          .controller!
          .text,
      'synthetic@example.test',
    );
    expect(
      tester
          .widget<TextFormField>(find.byType(TextFormField).at(1))
          .controller!
          .text,
      'synthetic-password',
    );
    expect(tester.takeException(), isNull);
  });
  testWidgets('empty academies shows real zero totals and empty state', (
    tester,
  ) async {
    final api = PlatformApi(
      'https://example.invalid',
      client: MockClient(
        (r) async => r.url.path.endsWith('/auth/login')
            ? response({'token': 'synthetic', 'name': 'Operador'})
            : response({'items': [], 'total': 0}),
      ),
    );
    await tester.pumpWidget(PlatformApp(api: api));
    await login(tester);
    for (var i = 0; i < 3; i++) {
      expect(tester.widget<Text>(find.byKey(ValueKey('metric-$i'))).data, '0');
    }
    expect(find.text('Nenhuma academia provisionada.'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  testWidgets(
    'status filter applies to list and remains selected after navigation',
    (tester) async {
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient(
          (r) async => r.url.path.endsWith('/auth/login')
              ? response({'token': 'synthetic', 'name': 'Operador'})
              : response({
                  'items': [academy(1), academy(2, completed: true)],
                  'total': 2,
                }),
        ),
      );
      await tester.pumpWidget(PlatformApp(api: api));
      await login(tester);
      await navigate(tester, 1);
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Concluídas').last);
      await tester.pumpAndSettle();
      expect(find.text('Academia 2'), findsOneWidget);
      expect(find.text('Academia 1'), findsNothing);
      await navigate(tester, 2);
      expect(find.text('Academia 2'), findsOneWidget);
      expect(find.text('Academia 1'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'contextual onboarding actions preserve version and completion rules',
    (tester) async {
      final a = academy(1)
        ..['adminActivatedAt'] = '2026-10-12T12:00:00Z'
        ..['status'] = 'configuration';
      final bodies = <Map<String, dynamic>>[];
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          if (r.method == 'PATCH') {
            final body = jsonDecode(r.body) as Map<String, dynamic>;
            bodies.add(body);
            expect(body['version'], a['version']);
            expect(body['evidence'], isNull);
            if (body['step'] == 'configuration') {
              a['configurationConfirmed'] = true;
              a['status'] = 'ready';
            }
            if (body['step'] == 'complete') {
              a['completedAt'] = '2026-10-12T13:00:00Z';
              a['status'] = 'completed';
            }
            a['version'] = (a['version'] as int) + 1;
            return response({});
          }
          return response({'academy': a, 'audit': []});
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(useMaterial3: true),
          home: Scaffold(
            body: AcademyDialog(api: api, gymId: 'synthetic-1'),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Reemitir convite'), findsNothing);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Concluir implantação'),
            )
            .onPressed,
        isNull,
      );
      await tester.ensureVisible(find.text('Confirmar dados iniciais'));
      await tester.tap(find.text('Confirmar dados iniciais'));
      await tester.pumpAndSettle();
      expect(find.text('Confirmar dados iniciais'), findsNothing);
      await tester.ensureVisible(find.text('Concluir implantação'));
      await tester.tap(find.text('Concluir implantação'));
      await tester.pumpAndSettle();
      expect(bodies.map((b) => b['step']).toList(), [
        'configuration',
        'complete',
      ]);
      expect(find.text('Concluir implantação'), findsNothing);
      expect(find.text('Implantação concluída'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
}
