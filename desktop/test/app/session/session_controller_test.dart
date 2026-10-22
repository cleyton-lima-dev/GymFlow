import 'dart:async';
import 'dart:convert';

import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import '../../support/test_session_storage.dart';

Map<String, Object> identity(String role) => {
  'userId': 'synthetic-user',
  'gymId': 'synthetic-gym',
  'name': 'Synthetic Admin',
  'email': 'admin@example.invalid',
  'role': role,
  'token': 'synthetic-token',
};

const admin = SessionUser(
  userId: 'synthetic-user',
  gymId: 'synthetic-gym',
  name: 'Admin',
  email: 'admin@example.invalid',
  role: AppRole.admin,
);

void main() {
  late FlutterSecureStoragePlatform original;
  late TestFlutterSecureStoragePlatform platform;
  late TestSessionRestorationGuard guard;

  setUp(() {
    original = FlutterSecureStoragePlatform.instance;
    platform = TestFlutterSecureStoragePlatform({});
    FlutterSecureStoragePlatform.instance = platform;
    guard = TestSessionRestorationGuard();
  });
  tearDown(() {
    FlutterSecureStoragePlatform.instance = original;
  });

  SessionController sessionFor(ApiClient api) {
    final session = SessionController(
      TokenStorage(const FlutterSecureStorage(), restorationGuard: guard),
      AuthService(api),
      api,
    );
    api.setUnauthorizedHandler(session.invalidateSession);
    addTearDown(session.dispose);
    addTearDown(api.close);
    return session;
  }

  for (final role in ['Professor', 'Student']) {
    test('$role login is rejected without persisting a credential', () async {
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient(
          (_) async => http.Response(jsonEncode(identity(role)), 200),
        ),
      );
      final session = sessionFor(api);
      await expectLater(
        session.login(
          email: 'synthetic@example.invalid',
          password: 'synthetic-password',
        ),
        throwsA(isA<ManagementAccessDeniedException>()),
      );
      expect(session.status, SessionStatus.unauthenticated);
      expect(session.user, isNull);
      expect(platform.data, isEmpty);
    });

    test(
      '$role persisted login cannot bootstrap into the management UI',
      () async {
        platform.data['auth_token'] = 'synthetic-legacy-token';
        final api = ApiClient(
          baseUrl: 'https://synthetic.invalid',
          client: MockClient(
            (_) async => http.Response(jsonEncode(identity(role)), 200),
          ),
        );
        final session = sessionFor(api);
        await session.bootstrap();
        expect(session.status, SessionStatus.unauthenticated);
        expect(session.user, isNull);
        expect(platform.data, isEmpty);
        expect(guard.blocked, isTrue);
      },
    );
  }

  test(
    '401 on bootstrap clears identity, HTTP and persistent restoration',
    () async {
      platform.data['auth_token'] = 'synthetic-expired-token';
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient((_) async => http.Response('', 401)),
      );
      final session = sessionFor(api);
      await session.bootstrap();
      expect(session.status, SessionStatus.unauthenticated);
      expect(session.user, isNull);
      expect(platform.data, isEmpty);
      expect(guard.blocked, isTrue);
    },
  );

  test(
    'late login after logout cannot recreate an authenticated session',
    () async {
      final requested = Completer<void>();
      final response = Completer<http.Response>();
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient((_) {
          requested.complete();
          return response.future;
        }),
      );
      final session = sessionFor(api);
      final login = session.login(
        email: 'synthetic@example.invalid',
        password: 'synthetic-password',
      );
      await requested.future;
      await session.logout();
      response.complete(http.Response(jsonEncode(identity('Admin')), 200));
      await login;
      expect(session.status, SessionStatus.unauthenticated);
      expect(session.user, isNull);
      expect(platform.data, isEmpty);
      expect(guard.blocked, isTrue);
    },
  );

  test('late bootstrap after a session replacement cannot restore the older identity', () async {
    platform.data['auth_token'] = 'synthetic-legacy-token';
    final requested = Completer<void>();
    final response = Completer<http.Response>();
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((_) {
        requested.complete();
        return response.future;
      }),
    );
    final session = sessionFor(api);
    final boot = session.bootstrap();
    await requested.future;
    await session.startSession(
      user: const SessionUser(
        userId: 'second-user',
        gymId: 'same-gym',
        name: 'New Admin',
        email: 'other@example.invalid',
        role: AppRole.admin,
      ),
      token: 'second-user-token',
    );
    response.complete(http.Response(jsonEncode(identity('Admin')), 200));
    await boot;
    expect(session.status, SessionStatus.authenticated);
    expect(session.user!.userId, 'second-user');
    expect(platform.data['auth_token'], 'second-user-token');
  });

  test(
    'bootstrap network error cannot leave a usable HTTP credential in memory',
    () async {
      platform.data['auth_token'] = 'synthetic-legacy-token';
      var fail = true;
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient((request) async {
          if (fail) return http.Response('', 500);
          expect(request.headers.containsKey('authorization'), isFalse);
          return http.Response('{}', 200);
        }),
      );
      final session = sessionFor(api);
      await expectLater(session.bootstrap(), throwsException);
      expect(session.user, isNull);
      expect(session.status, SessionStatus.unknown);
      fail = false;
      await api.get('api/probe');
    },
  );

  test('public startSession also refuses a non-admin identity', () async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((_) async => http.Response('{}', 200)),
    );
    final session = sessionFor(api);
    await expectLater(
      session.startSession(
        user: const SessionUser(
          userId: 'student',
          gymId: 'gym',
          name: 'Student',
          email: 'student@example.invalid',
          role: AppRole.student,
        ),
        token: 'synthetic-token',
      ),
      throwsA(isA<ManagementAccessDeniedException>()),
    );
    expect(session.status, SessionStatus.unauthenticated);
    expect(platform.data, isEmpty);
  });
}
