import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/gym_settings/data/gym_settings_service.dart';
import 'package:avelri_gestao/features/gym_settings/presentation/gym_settings_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

Map<String, dynamic> agent(bool release) => {
  'id': 'synthetic-agent',
  'gymId': 'synthetic-gym',
  'name': 'Agente sintético',
  'machineName': 'Do not display machine details',
  'isActive': true,
  'releaseEnabled': release,
  'configurationVersion': 2,
  'appliedConfigurationVersion': 1,
  'configurationApplied': false,
  'isOnline': false,
  'pendingOfflineEvents': 0,
  'desiredDevices': [],
  'devices': [
    {
      'deviceKey': 'technical-device-key',
      'providerKey': 'unconfirmed-provider',
      'enabled': true,
      'connected': false,
      'endpoint': 'do-not-display-endpoint',
    },
  ],
  'createdAt': '2026-10-12T00:00:00Z',
  'secretHash': 'do-not-display-secret',
};

Widget app(ApiClient api) => MultiProvider(
  providers: [
    Provider<ApiClient>.value(value: api),
    ChangeNotifierProvider(
      create: (_) => BrandingController(
        const LocalBrandingRepository({}),
        defaultBranding: avelriDefaultBranding,
      ),
    ),
  ],
  child: const MaterialApp(
    home: Scaffold(
      body: Padding(
        // Reserve the same space as the existing Desktop sidebar and header.
        padding: EdgeInsets.only(left: 254, top: 64),
        child: GymSettingsPage(),
      ),
    ),
  ),
);

http.Response jsonResponse(Object value) => http.Response(
  jsonEncode(value),
  200,
  headers: {'content-type': 'application/json'},
);

Future<void> selectSection(WidgetTester tester, String label) async {
  final dropdown = find.byKey(const ValueKey('settings-section-selector'));
  if (dropdown.evaluate().isNotEmpty) {
    await tester.tap(dropdown);
    await tester.pumpAndSettle();
    await tester.tap(find.text(label).last);
  } else {
    await tester.tap(find.widgetWithText(ListTile, label));
  }
  await tester.pumpAndSettle();
}

void main() {
  test('settings use authenticated endpoint and reject malformed configuration responses', () async {
    final api = ApiClient(
      baseUrl: 'https://example.invalid/',
      client: MockClient((request) async {
        expect(request.method, 'GET');
        expect(request.url.path, '/api/gym-settings');
        expect(request.url.hasQuery, isFalse);
        expect(request.headers['Authorization'], 'Bearer synthetic-token');
        return jsonResponse({
          'timeZoneId': 'America/Sao_Paulo',
          'timeZoneStatus': 'configured',
        });
      }),
    );
    api.setAccessToken('synthetic-token');
    expect(
      (await GymSettingsService(api).get()).timeZoneId,
      'America/Sao_Paulo',
    );
    for (final invalid in [
      {'timeZoneStatus': 'configured'},
      {'timeZoneStatus': 'unknown'},
    ]) {
      expect(() => GymSettings.fromJson(invalid), throwsFormatException);
    }
    api.close();
  });

  for (final size in [
    const Size(800, 600),
    const Size(1024, 720),
    const Size(1366, 768),
    const Size(1920, 1080),
  ]) {
    testWidgets('settings are readable without overflow at $size', (
      tester,
    ) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = ApiClient(
        baseUrl: 'https://example.invalid/',
        client: MockClient(
          (r) async => r.url.path.endsWith('gym-settings')
              ? jsonResponse({
                  'timeZoneId': 'America/Sao_Paulo',
                  'timeZoneStatus': 'configured',
                })
              : jsonResponse([agent(false)]),
        ),
      );
      await tester.pumpWidget(app(api));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextFormField>(find.byType(TextFormField).last)
            .controller!
            .text,
        'America/Sao_Paulo',
      );

      expect(find.textContaining('do-not-display'), findsNothing);
      expect(find.text('technical-device-key'), findsNothing);
      expect(find.text('unconfirmed-provider'), findsNothing);
      expect(find.byType(TextField), findsNWidgets(2));
      expect(
        tester
            .widget<TextFormField>(find.byType(TextFormField).first)
            .controller!
            .text,
        isEmpty,
      );
      expect(find.textContaining('ErpEnabled'), findsNothing);
      await tester.enterText(
        find.byType(TextFormField).first,
        'Nome em edição',
      );
      await selectSection(tester, 'Controle de acesso');
      expect(find.text('Agente sintético'), findsOneWidget);
      expect(find.byType(TextFormField), findsNothing);
      await tester.ensureVisible(find.text('Abrir controle de acesso'));
      expect(tester.takeException(), isNull);
      await selectSection(tester, 'Dados da academia');
      expect(find.text('Nome em edição'), findsOneWidget);
      tester.view.physicalSize = size.width < 1200
          ? const Size(1920, 1080)
          : const Size(800, 600);
      await tester.pumpAndSettle();
      expect(find.text('Nome em edição'), findsOneWidget);
      await selectSection(tester, 'Controle de acesso');
      expect(find.text('Agente sintético'), findsOneWidget);
      tester.view.physicalSize = size;
      await tester.pumpAndSettle();
      expect(find.text('Agente sintético'), findsOneWidget);
      await selectSection(tester, 'Dados da academia');
      expect(find.text('Agente sintético'), findsNothing);
      if (size.width - 254 >= 960) {
        expect(
          tester
              .widget<ListTile>(
                find.widgetWithText(ListTile, 'Dados da academia'),
              )
              .selected,
          isTrue,
        );
      } else {
        expect(
          find.byKey(const ValueKey('settings-section-selector')),
          findsOneWidget,
        );
      }
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      api.close();
    });
  }

  testWidgets(
    'independent loading error retry missing and invalid timezone states',
    (tester) async {
      var phase = 0;
      final pending = Completer<http.Response>();
      final api = ApiClient(
        baseUrl: 'https://example.invalid/',
        client: MockClient((r) async {
          if (r.url.path.endsWith('access-agents')) return jsonResponse([]);
          if (phase == 0) return pending.future;
          return jsonResponse({
            'timeZoneId': null,
            'timeZoneStatus': phase == 1 ? 'missing' : 'invalid',
          });
        }),
      );
      await tester.pumpWidget(app(api));
      await tester.pump();
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
      pending.complete(http.Response('sensitive-internal-error', 500));
      await tester.pumpAndSettle();
      expect(
        find.text('Não foi possível consultar o fuso horário.'),
        findsOneWidget,
      );

      expect(find.textContaining('sensitive-internal'), findsNothing);
      phase = 1;
      await tester.tap(find.text('Tentar novamente'));
      await tester.pumpAndSettle();
      expect(find.text('Fuso horário não configurado'), findsOneWidget);
      phase = 2;
      await tester.tap(find.byTooltip('Atualizar configurações'));
      await tester.pumpAndSettle();
      expect(find.text('Fuso horário inválido'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      api.close();
    },
  );

  testWidgets(
    'existing release confirmation saves only operational setting and refreshes',
    (tester) async {
      tester.view.physicalSize = const Size(1366, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      var release = true;
      var writes = 0;
      final saving = Completer<http.Response>();
      final api = ApiClient(
        baseUrl: 'https://example.invalid/',
        client: MockClient((r) async {
          if (r.method == 'PATCH') {
            writes++;
            expect(r.url.path, '/api/access-agents/synthetic-agent/release');
            expect(jsonDecode(r.body), {'enabled': false});
            return saving.future;
          }
          return r.url.path.endsWith('gym-settings')
              ? jsonResponse({
                  'timeZoneId': 'UTC',
                  'timeZoneStatus': 'configured',
                })
              : jsonResponse([agent(release)]);
        }),
      );
      await tester.pumpWidget(app(api));
      await tester.pumpAndSettle();
      await selectSection(tester, 'Controle de acesso');
      await tester.ensureVisible(find.text('Desativar liberação física'));
      await tester.tap(find.text('Desativar liberação física'));
      await tester.pumpAndSettle();
      expect(writes, 0);
      await tester.tap(find.text('Desativar liberação'));
      await tester.pump();
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      release = false;
      saving.complete(jsonResponse(agent(false)));
      await tester.pumpAndSettle();
      expect(writes, 1);
      expect(find.text('Liberação física desativada'), findsOneWidget);
      expect(
        find.textContaining('Configuração operacional salva.'),
        findsOneWidget,
      );
      await tester.pumpWidget(const SizedBox());
      api.close();
    },
  );
}
