import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/web/activation_route_compat.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:gymflow/features/auth/presentation/activate_invite_page.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('maps only direct activation URLs to the existing hash route', () {
    final activationUrl = Uri.parse(
      'https://avelri.pages.dev/ativar?token=synthetic&source=desktop',
    );
    expect(
      activationHashRouteForDirectUrl(activationUrl).toString(),
      'https://avelri.pages.dev/#/ativar?token=synthetic&source=desktop',
    );
    expect(
      activationHashRouteWithoutToken(
        Uri.parse(
          'https://avelri.pages.dev/#/ativar?token=synthetic&source=desktop',
        ),
      ).toString(),
      'https://avelri.pages.dev/#/ativar?source=desktop',
    );
    expect(
      activationHashRouteForDirectUrl(
        Uri.parse('https://avelri.pages.dev/login'),
      ),
      isNull,
    );
    expect(
      activationHashRouteForDirectUrl(
        Uri.parse('https://avelri.pages.dev/#/ativar?token=synthetic'),
      ),
      isNull,
    );
  });

  testWidgets('validates invite and lets the student set their own password', (
    tester,
  ) async {
    final requests = <http.Request>[];
    final client = ApiClient(
      baseUrl: 'https://api.example.test/',
      client: MockClient((request) async {
        requests.add(request);
        if (request.url.path.endsWith('/validate')) {
          return http.Response('{"valid":true}', 200);
        }
        return http.Response('', 204);
      }),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: ActivateInvitePage(
          authService: AuthService(client),
          token: 'synthetic-invite-token',
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Crie sua senha'), findsOneWidget);

    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), 'senha-segura-123');
    await tester.enterText(fields.at(1), 'senha-segura-123');
    await tester.tap(find.text('Ativar acesso'));
    await tester.pumpAndSettle();

    expect(find.text('Acesso ativado'), findsOneWidget);
    expect(requests.map((request) => request.url.path), [
      '/api/students/app-activate/validate',
      '/api/students/app-activate',
    ]);
    expect(jsonDecode(requests.last.body), {
      'token': 'synthetic-invite-token',
      'password': 'senha-segura-123',
    });
  });

  testWidgets('shows an expired or revoked invite message', (tester) async {
    final client = ApiClient(
      baseUrl: 'https://api.example.test/',
      client: MockClient(
        (_) async => http.Response(
          '{"valid":false,"message":"Este convite expirou. Solicite um novo convite ao Admin."}',
          400,
        ),
      ),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: ActivateInvitePage(
          authService: AuthService(client),
          token: 'expired-invite-token',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('Este convite expirou'), findsOneWidget);
  });
}
