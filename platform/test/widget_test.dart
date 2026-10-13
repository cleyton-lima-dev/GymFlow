import 'dart:convert';

import 'package:avelri_platform/platform_app.dart';
import 'package:avelri_platform/platform_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

http.Response json(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

void main() {
  test(
    'API isolates operator session, sanitizes errors and sends idempotency key',
    () async {
      var calls = 0;
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          expect(r.headers['Authorization'], 'Bearer synthetic-token');
          expect(r.headers['Idempotency-Key'], 'synthetic-key');
          calls++;
          return http.Response('internal-secret-must-not-appear', 409);
        }),
      );
      api.token = 'synthetic-token';
      await expectLater(
        api.request(
          'POST',
          '/api/platform/gym-onboardings',
          requestKey: 'synthetic-key',
          body: {},
        ),
        throwsA(
          isA<PlatformFailure>().having(
            (e) => e.message.contains('internal-secret'),
            'sanitized',
            false,
          ),
        ),
      );
      expect(calls, 1);
      expect(
        PlatformApi.requestKey(),
        matches(RegExp(r'^[a-f0-9]{8}(-[a-f0-9]{4}){3}-[a-f0-9]{12}$')),
      );
    },
  );

  test('expired session is removed from memory', () async {
    var expired = false;
    final api = PlatformApi(
      'https://example.invalid',
      client: MockClient((r) async => http.Response('', 401)),
    );
    api.token = 'synthetic';
    api.onSessionExpired = () => expired = true;
    await expectLater(
      api.request('GET', '/api/platform/gym-onboardings'),
      throwsA(isA<PlatformFailure>()),
    );
    expect(api.token, isNull);
    expect(expired, isTrue);
  });

  for (final size in [
    const Size(800, 600),
    const Size(1366, 768),
    const Size(1920, 1080),
    const Size(360, 800),
  ]) {
    testWidgets('operator login, academy listing and detail at $size', (
      tester,
    ) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final academy = {
        'gymId': 'synthetic-gym',
        'administrativeName': 'Academia sintética',
        'adminName': 'Admin sintético',
        'adminEmail': 'admin@example.test',
        'timeZoneId': 'America/Sao_Paulo',
        'status': 'awaiting-admin',
        'adminActivatedAt': null,
        'completedAt': null,
        'configurationConfirmed': false,
        'physicalAccessContracted': true,
        'physicalValidationEvidence': null,
        'version': 1,
      };
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          if (r.url.path.endsWith('/auth/login')) {
            expect(jsonDecode(r.body)['code'], '123456');
            return json({
              'token': 'synthetic-token',
              'name': 'Operador sintético',
            });
          }
          expect(r.headers['Authorization'], 'Bearer synthetic-token');
          if (r.url.path.endsWith('/synthetic-gym')) {
            return json({'academy': academy, 'audit': []});
          }
          return json({
            'items': [academy],
            'total': 1,
          });
        }),
      );
      await tester.pumpWidget(PlatformApp(api: api));
      expect(find.text('Código do autenticador'), findsOneWidget);
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
      expect(find.text('Academia sintética'), findsOneWidget);
      expect(find.text('synthetic-gym'), findsNothing);
      await tester.ensureVisible(find.text('Academia sintética'));
      await tester.tap(find.text('Academia sintética'));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('não comprovam passagem física'),
        findsOneWidget,
      );
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Concluir implantação'),
            )
            .onPressed,
        isNull,
      );
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    });
  }

  testWidgets(
    'activation requires confirmation and sends only token and password',
    (tester) async {
      var writes = 0;
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          expect(r.url.path, '/api/onboarding/admin-activation');
          expect(jsonDecode(r.body), {
            'token': 'synthetic-invite',
            'password': 'synthetic-password',
          });
          expect(r.headers['Authorization'], isNull);
          writes++;
          return http.Response('', 204);
        }),
      );
      await tester.pumpWidget(
        PlatformApp(api: api, activationToken: 'synthetic-invite'),
      );
      await tester.enterText(
        find.byType(TextFormField).first,
        'synthetic-password',
      );
      await tester.tap(find.text('Definir senha e ativar'));
      await tester.pumpAndSettle();
      expect(writes, 0);
      expect(find.text('As senhas devem ser iguais.'), findsOneWidget);
      await tester.enterText(
        find.byType(TextFormField).last,
        'synthetic-password',
      );
      await tester.tap(find.text('Definir senha e ativar'));
      await tester.pumpAndSettle();
      expect(writes, 1);
      expect(find.textContaining('Senha configurada'), findsOneWidget);
      expect(find.text('synthetic-invite'), findsNothing);
    },
  );

  testWidgets(
    'provision retry keeps request key and body without automatic retry',
    (tester) async {
      final keys = <String>[];
      final bodies = <String>[];
      final api = PlatformApi(
        'https://example.invalid',
        client: MockClient((r) async {
          keys.add(r.headers['Idempotency-Key']!);
          bodies.add(r.body);
          return http.Response('', 500);
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Builder(
              builder: (context) => TextButton(
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (_) => ProvisionDialog(api: api),
                ),
                child: const Text('Abrir'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Abrir'));
      await tester.pumpAndSettle();
      final values = [
        'Academia sintética',
        'America/Sao_Paulo',
        'Admin sintético',
        'admin@example.test',
      ];
      for (var i = 0; i < values.length; i++) {
        await tester.enterText(find.byType(TextFormField).at(i), values[i]);
      }
      await tester.tap(find.text('Provisionar'));
      await tester.pumpAndSettle();
      expect(keys, hasLength(1));
      await tester.tap(find.text('Repetir solicitação'));
      await tester.pumpAndSettle();
      expect(keys, hasLength(2));
      expect(keys.first, keys.last);
      expect(bodies.first, bodies.last);
      expect((jsonDecode(bodies.first) as Map).containsKey('gymId'), isFalse);
      expect(
        (jsonDecode(bodies.first) as Map).containsKey('erpEnabled'),
        isFalse,
      );
    },
  );
}
