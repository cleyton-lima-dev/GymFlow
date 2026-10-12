import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/gym_settings/data/gym_settings_service.dart';
import 'package:avelri_gestao/features/gym_settings/presentation/gym_settings_form.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  testWidgets(
    'first save validates name and sends version without tenant or ERP switches',
    (tester) async {
      Map<String, dynamic>? body;
      GymSettings? saved;
      final api = ApiClient(
        baseUrl: 'https://example.invalid/',
        client: MockClient((r) async {
          expect(r.method, 'PUT');
          body = jsonDecode(r.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'administrativeName': body!['administrativeName'],
              'timeZoneId': body!['timeZoneId'],
              'timeZoneStatus': 'configured',
              'version': 1,
              'timeZoneSource': 'persisted',
            }),
            200,
          );
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: GymSettingsForm(
              settings: const GymSettings(
                timeZoneId: 'UTC',
                timeZoneStatus: 'configured',
              ),
              service: GymSettingsService(api),
              onSaved: (value) => saved = value,
              onReload: () {},
            ),
          ),
        ),
      );
      await tester.tap(find.text('Salvar configurações'));
      await tester.pumpAndSettle();
      expect(find.text('Informe o nome fantasia.'), findsOneWidget);
      expect(body, isNull);
      await tester.enterText(
        find.byType(TextFormField).first,
        ' Academia sintética ',
      );
      await tester.tap(find.text('Salvar configurações'));
      await tester.pumpAndSettle();
      expect(body, {
        'administrativeName': 'Academia sintética',
        'timeZoneId': 'UTC',
        'version': null,
        'confirmTimeZoneChange': false,
      });
      expect(saved!.version, 1);
      expect(find.byType(AlertDialog), findsNothing);
      await tester.pumpWidget(const SizedBox());
      api.close();
    },
  );

  testWidgets(
    'timezone change requires visual confirmation and can be cancelled',
    (tester) async {
      var writes = 0;
      final pending = Completer<http.Response>();
      final api = ApiClient(
        baseUrl: 'https://example.invalid/',
        client: MockClient((r) {
          writes++;
          final body = jsonDecode(r.body) as Map;
          expect(body['version'], 3);
          expect(body['confirmTimeZoneChange'], isTrue);
          expect(body['timeZoneId'], 'America/Sao_Paulo');
          return pending.future;
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: GymSettingsForm(
              settings: const GymSettings(
                administrativeName: 'Synthetic',
                timeZoneId: 'UTC',
                timeZoneStatus: 'configured',
                version: 3,
              ),
              service: GymSettingsService(api),
              onSaved: (_) {},
              onReload: () {},
            ),
          ),
        ),
      );
      await tester.enterText(
        find.byType(TextFormField).last,
        'America/Sao_Paulo',
      );
      await tester.tap(find.text('Salvar configurações'));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('registros históricos não serão convertidos'),
        findsOneWidget,
      );
      expect(writes, 0);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(writes, 0);
      await tester.tap(find.text('Salvar configurações'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Confirmar alteração'));
      await tester.pump();
      expect(find.text('Salvando…'), findsOneWidget);
      expect(writes, 1);
      pending.complete(
        http.Response(
          jsonEncode({
            'timeZoneId': 'America/Sao_Paulo',
            'timeZoneStatus': 'configured',
            'administrativeName': 'Synthetic',
            'version': 4,
          }),
          200,
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      api.close();
    },
  );

  testWidgets(
    '409 preserves edits and requires reload without automatic retry',
    (tester) async {
      var writes = 0;
      var reloads = 0;
      final api = ApiClient(
        baseUrl: 'https://example.invalid/',
        client: MockClient((r) async {
          writes++;
          return http.Response('{"message":"sensitive-internal-error"}', 409);
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: GymSettingsForm(
              settings: const GymSettings(
                administrativeName: 'Initial',
                timeZoneId: 'UTC',
                timeZoneStatus: 'configured',
                version: 1,
              ),
              service: GymSettingsService(api),
              onSaved: (_) => fail('Must not succeed'),
              onReload: () => reloads++,
            ),
          ),
        ),
      );
      await tester.enterText(find.byType(TextFormField).first, 'Edited');
      await tester.tap(find.text('Salvar configurações'));
      await tester.pumpAndSettle();
      expect(writes, 1);
      expect(find.text('Edited'), findsOneWidget);
      expect(find.textContaining('sensitive-internal'), findsNothing);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Salvar configurações'),
            )
            .onPressed,
        isNull,
      );
      await tester.tap(find.text('Recarregar configurações'));
      await tester.pumpAndSettle();
      expect(reloads, 1);
      expect(writes, 1);
      await tester.pumpWidget(const SizedBox());
      api.close();
    },
  );
}
