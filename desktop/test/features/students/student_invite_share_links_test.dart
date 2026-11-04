import 'dart:convert';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/branding_repository.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/presentation/student_details_page.dart';
import 'package:avelri_gestao/features/students/presentation/student_invite_share_links.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../support/management_test_scope.dart';

void main() {
  test('email and WhatsApp prepare the same secure invite and login email', () {
    const email = 'aluno@example.test';
    const invite = 'https://example.test/ativar?token=synthetic-token';
    final expectedMessage =
        'Olá! Seu acesso ao Avelri Gestão está liberado. '
        'Ative sua conta e defina sua senha neste link: $invite\n\n'
        'Use $email como e-mail de login. O convite expira em 24 horas.';

    final emailUri = StudentInviteShareLinks.email(
      email: email,
      inviteLink: invite,
    );
    expect(emailUri.scheme, 'mailto');
    expect(emailUri.path, email);
    expect(emailUri.queryParameters['body'], expectedMessage);
    expect(emailUri.queryParameters['body'], isNot(contains('senha temporária')));

    final whatsappUri = StudentInviteShareLinks.whatsapp(
      email: email,
      inviteLink: invite,
    );
    expect(whatsappUri.scheme, 'https');
    expect(whatsappUri.host, 'wa.me');
    expect(whatsappUri.queryParameters['text'], expectedMessage);
  });

  testWidgets('Admin can prepare both invitation channels after entering email', (
    tester,
  ) async {
    final bodies = <Map<String, dynamic>>[];
    var inviteNumber = 0;
    final openedLinks = <Uri>[];
    final apiClient = ApiClient(
      baseUrl: 'https://synthetic.example/',
      client: MockClient((request) async {
        if (request.method == 'GET' &&
            request.url.path == '/api/students/student-1') {
          return _response({
            'id': 'student-1',
            'name': 'Aluno sintético',
            'email': null,
            'phone': null,
            'birthDate': null,
            'isActive': true,
            'createdAt': '2026-01-01T00:00:00Z',
            'enrollmentStatus': null,
            'planName': null,
            'enrollmentEndDate': null,
            'archivedAt': null,
            'appAccessEnabled': false,
            'userId': null,
          });
        }
        if (request.method == 'GET' &&
            request.url.path.endsWith('/financial-history')) {
          return _response([]);
        }
        if (request.method == 'GET' &&
            request.url.path.endsWith(
              '/physical-access/credentials/student/student-1',
            )) {
          return _response([]);
        }
        if (request.method == 'GET' &&
            request.url.path.endsWith(
              '/physical-access/overrides/student/student-1',
            )) {
          return _response({'message': 'not found'}, statusCode: 404);
        }
        if (request.method == 'POST' &&
            request.url.path == '/api/students/student-1/app-access') {
          bodies.add(jsonDecode(request.body) as Map<String, dynamic>);
          inviteNumber++;
          return _response({
            'enabled': true,
            'token': 'synthetic-token-$inviteNumber',
            'expiresAt': '2026-01-02T00:00:00Z',
          });
        }
        throw StateError('Unexpected request ${request.method} ${request.url}');
      }),
    );
    final brandingController = BrandingController(
      _TestBrandingRepository(),
      defaultBranding: const GymBranding(
        id: 'test',
        displayName: 'Avelri Test',
        primaryColor: Colors.blue,
        secondaryColor: Colors.lightBlue,
        backgroundColor: Colors.white,
        brightness: Brightness.light,
      ),
    );

    await tester.pumpWidget(
      ManagementTestScope(
        api: apiClient,
        erpEnabled: false,
        child: MultiProvider(
          providers: [
            ChangeNotifierProvider<BrandingController>.value(
              value: brandingController,
            ),
          ],
          child: MaterialApp(
            home: Scaffold(
              body: StudentDetailsPage(
                studentId: 'student-1',
                launchInviteUrl: (uri) async {
                  openedLinks.add(uri);
                  return true;
                },
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Habilitar acesso'));
    await tester.tap(find.text('Habilitar acesso'));
    await tester.pumpAndSettle();
    expect(find.text('E-mail para acesso ao aplicativo'), findsOneWidget);
    await tester.enterText(find.byType(TextFormField), '  ALUNO@EXAMPLE.TEST  ');
    await tester.tap(find.text('Continuar'));
    await tester.pumpAndSettle();

    expect(find.text('Abrir e-mail'), findsOneWidget);
    await tester.tap(find.text('Abrir e-mail'));
    await tester.pumpAndSettle();
    expect(openedLinks.single.scheme, 'mailto');
    expect(openedLinks.single.path, 'aluno@example.test');
    expect(bodies.single, {
      'enabled': true,
      'email': 'aluno@example.test',
    });
    expect(bodies.single, isNot(contains('password')));
    expect(bodies.single, isNot(contains('invitation')));

    await tester.ensureVisible(find.text('Reemitir convite'));
    await tester.tap(find.text('Reemitir convite'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Abrir WhatsApp'));
    await tester.pumpAndSettle();

    expect(openedLinks, hasLength(2));
    expect(openedLinks.last.scheme, 'https');
    expect(openedLinks.last.host, 'wa.me');
    expect(openedLinks.last.queryParameters['text'], contains('aluno@example.test'));
    expect(bodies, hasLength(2));
    expect(bodies.last, {'enabled': true});

    String? copiedInvite;
    var clipboardWrites = 0;
    final messenger =
        TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;
    messenger.setMockMethodCallHandler(SystemChannels.platform, (call) async {
      if (call.method == 'Clipboard.setData') {
        clipboardWrites++;
        copiedInvite = (call.arguments as Map<Object?, Object?>)['text'] as String?;
      }
      return null;
    });
    addTearDown(() {
      messenger.setMockMethodCallHandler(SystemChannels.platform, null);
    });

    await tester.ensureVisible(find.text('Reemitir convite'));
    await tester.tap(find.text('Reemitir convite'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Copiar convite'));
    await tester.pumpAndSettle();

    expect(clipboardWrites, 1);
    const appBaseUrl = String.fromEnvironment(
      'STUDENT_APP_URL',
      defaultValue: 'https://avelri.pages.dev',
    );
    expect(
      copiedInvite,
      '$appBaseUrl/ativar?token=synthetic-token-3',
    );
    expect(find.text('Convite para o aplicativo'), findsNothing);
  });
}

http.Response _response(Object? body, {int statusCode = 200}) => http.Response(
  jsonEncode(body),
  statusCode,
  headers: {'content-type': 'application/json'},
);

class _TestBrandingRepository implements BrandingRepository {
  @override
  Future<GymBranding?> getByGymId(String gymId) async => null;
}
