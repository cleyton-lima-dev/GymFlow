import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/core/storage/session_restoration_guard.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import '../../support/test_session_storage.dart';

void main() {
  late FlutterSecureStoragePlatform originalPlatform;
  late RecordingSecureStoragePlatform platform;
  late TargetPlatform? originalTargetPlatform;
  late Directory markerDirectory;
  late FileSessionRestorationGuard guard;

  setUp(() async {
    markerDirectory = await Directory.systemTemp.createTemp(
      'avelri-session-test-',
    );
    guard = FileSessionRestorationGuard.inDirectory(markerDirectory);
    originalPlatform = FlutterSecureStoragePlatform.instance;
    originalTargetPlatform = debugDefaultTargetPlatformOverride;
    debugDefaultTargetPlatformOverride = TargetPlatform.windows;
    platform = RecordingSecureStoragePlatform({
      'auth_token': 'legacy-jwt-from-windows-credential-manager',
    });
    FlutterSecureStoragePlatform.instance = platform;
  });

  tearDown(() async {
    FlutterSecureStoragePlatform.instance = originalPlatform;
    debugDefaultTargetPlatformOverride = originalTargetPlatform;
    await markerDirectory.delete(recursive: true);
  });

  test(
    'credential read, write and logout deletion use protected storage',
    () async {
      const storage = FlutterSecureStorage(
        wOptions: WindowsOptions(useBackwardCompatibility: true),
      );
      final tokens = TokenStorage(storage, restorationGuard: guard);

      await tokens.saveToken('jwt-secret');
      expect(await tokens.readToken(), 'jwt-secret');
      expect(platform.data, {'auth_token': 'jwt-secret'});
      expect(platform.lastOptions['useBackwardCompatibility'], 'true');

      await tokens.deleteToken();
      expect(platform.data, isEmpty);
      expect(platform.lastOptions['useBackwardCompatibility'], 'true');
    },
  );

  test('legacy token restores and forwards Windows compatibility option (platform fake)', () async {
    const secureStorage = FlutterSecureStorage(
      wOptions: WindowsOptions(useBackwardCompatibility: true),
    );
    final tokens = TokenStorage(secureStorage, restorationGuard: guard);
    final apiClient = ApiClient(
      baseUrl: 'https://api.example.invalid/',
      client: MockClient((request) async {
        expect(request.url.path, '/api/auth/me');
        expect(
          request.headers['authorization'],
          'Bearer legacy-jwt-from-windows-credential-manager',
        );
        return http.Response(
          jsonEncode({
            'userId': 'user-id',
            'gymId': 'gym-id',
            'name': 'Legacy Admin',
            'email': 'admin@example.invalid',
            'role': 'Admin',
          }),
          200,
        );
      }),
    );
    final session = SessionController(
      tokens,
      AuthService(apiClient),
      apiClient,
    );
    addTearDown(apiClient.close);
    addTearDown(session.dispose);

    await session.bootstrap();

    expect(session.status, SessionStatus.authenticated);
    expect(session.user?.role, AppRole.admin);
    expect(platform.lastOptions['useBackwardCompatibility'], 'true');
  });

  test('failed deletion leaves durable logout intent and cannot restore in a fresh session', () async {
    platform.failDeletes = true;
    const secureStorage = FlutterSecureStorage(
      wOptions: WindowsOptions(useBackwardCompatibility: true),
    );
    final tokens = TokenStorage(secureStorage, restorationGuard: guard);
    final observedAuthorization = <String?>[];
    final apiClient = ApiClient(
      baseUrl: 'https://api.example.invalid/',
      client: MockClient((request) async {
        observedAuthorization.add(request.headers['authorization']);
        return http.Response('{}', 200);
      }),
    );
    final session = SessionController(
      tokens,
      AuthService(apiClient),
      apiClient,
    );
    await session.startSession(
      user: const SessionUser(
        userId: 'user-id',
        gymId: 'gym-id',
        name: 'Admin',
        email: 'admin@example.invalid',
        role: AppRole.admin,
      ),
      token: 'jwt-secret',
    );

    await session.logout();

    expect(session.status, SessionStatus.unauthenticated);
    expect(session.user, isNull);
    expect(platform.data['auth_token'], 'jwt-secret');
    expect(await guard.isRestorationBlocked(), isTrue);
    expect(
      session.persistenceWarning,
      contains('restauração automática está bloqueada'),
    );
    await apiClient.get('api/probe');
    expect(observedAuthorization, [null]);

    // Recreate both objects, reading the actual flushed file. The secure
    // platform remains a fake; this does not homologate Windows CredMan.
    final restartedTokens = TokenStorage(
      secureStorage,
      restorationGuard: FileSessionRestorationGuard.inDirectory(
        markerDirectory,
      ),
    );
    final restarted = SessionController(
      restartedTokens,
      AuthService(apiClient),
      apiClient,
    );
    await restarted.bootstrap();
    expect(restarted.status, SessionStatus.unauthenticated);
    expect(restarted.user, isNull);
    expect(observedAuthorization, [null]);
    restarted.dispose();
    addTearDown(apiClient.close);
    addTearDown(session.dispose);
  });

  test('marker failure still deletes the credential and restart is unauthenticated', () async {
    final failingGuard = TestSessionRestorationGuard()..failBlock = true;
    final tokens = TokenStorage(
      const FlutterSecureStorage(),
      restorationGuard: failingGuard,
    );
    await expectLater(
      tokens.deleteToken(),
      throwsA(
        isA<TokenDeletionException>().having(
          (error) => error.credentialDeleted,
          'credentialDeleted',
          isTrue,
        ),
      ),
    );
    expect(platform.data, isEmpty);
    expect(await tokens.readToken(), isNull);
  });

  test(
    'dual persistence failure is explicit and clears HTTP and memory',
    () async {
      final failingGuard = TestSessionRestorationGuard();
      final api = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((request) async {
          expect(request.headers.containsKey('authorization'), isFalse);
          return http.Response('{}', 200);
        }),
      );
      final session = SessionController(
        TokenStorage(
          const FlutterSecureStorage(),
          restorationGuard: failingGuard,
        ),
        AuthService(api),
        api,
      );
      await session.startSession(
        user: const SessionUser(
          userId: 'admin',
          gymId: 'gym',
          name: 'Admin',
          email: 'admin@example.invalid',
          role: AppRole.admin,
        ),
        token: 'synthetic-token',
      );
      failingGuard.failBlock = true;
      platform.failDeletes = true;
      await session.logout();
      expect(session.status, SessionStatus.unauthenticated);
      expect(session.user, isNull);
      expect(
        session.persistenceWarning,
        contains('não foi possível impedir a restauração'),
      );
      await api.get('api/probe');
      platform.failDeletes = false;
      failingGuard.failBlock = false;
      await session.logout();
      expect(session.persistenceWarning, isNull);
      expect(platform.data, isEmpty);
      session.dispose();
      api.close();
    },
  );

  test(
    'unreadable restoration policy fails closed before reading a credential',
    () async {
      final failingGuard = TestSessionRestorationGuard()..failRead = true;
      final api = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((request) async {
          expect(request.headers.containsKey('authorization'), isFalse);
          return http.Response('{}', 200);
        }),
      )..setAccessToken('older-memory-token');
      final session = SessionController(
        TokenStorage(
          const FlutterSecureStorage(),
          restorationGuard: failingGuard,
        ),
        AuthService(api),
        api,
      );
      await expectLater(session.bootstrap(), throwsStateError);
      expect(session.status, SessionStatus.unknown);
      expect(session.user, isNull);
      expect(platform.readCount, 0);
      await api.get('api/probe');
      session.dispose();
      api.close();
    },
  );

  test(
    'failed secure read keeps bootstrap unauthenticated and HTTP clear',
    () async {
      platform.failReads = true;
      final api = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((request) async {
          expect(request.headers.containsKey('authorization'), isFalse);
          return http.Response('{}', 200);
        }),
      );
      final session = SessionController(
        TokenStorage(const FlutterSecureStorage(), restorationGuard: guard),
        AuthService(api),
        api,
      );
      await expectLater(session.bootstrap(), throwsStateError);
      expect(session.user, isNull);
      expect(session.status, SessionStatus.unknown);
      await api.get('api/probe');
      session.dispose();
      api.close();
    },
  );

  test('failed login completion cannot remove restoration block', () async {
    final failingGuard = TestSessionRestorationGuard()..failAllow = true;
    final tokens = TokenStorage(
      const FlutterSecureStorage(),
      restorationGuard: failingGuard,
    );
    await expectLater(
      tokens.saveToken('synthetic-new-token'),
      throwsStateError,
    );
    expect(platform.data['auth_token'], 'synthetic-new-token');
    expect(await tokens.readToken(), isNull);
  });

  test(
    'an empty on-disk logout marker also blocks legacy restoration',
    () async {
      await File(
        '${markerDirectory.path}${Platform.pathSeparator}session.signed-out',
      ).create();
      final tokens = TokenStorage(
        const FlutterSecureStorage(),
        restorationGuard: guard,
      );
      expect(await tokens.readToken(), isNull);
      expect(platform.readCount, 0);
    },
  );

  test(
    'logout queued during secure write cannot be undone by its late completion',
    () async {
      final delayedPlatform = DelayedSecureStoragePlatform({})
        ..failDeletes = true;
      FlutterSecureStoragePlatform.instance = delayedPlatform;
      final tokens = TokenStorage(
        const FlutterSecureStorage(),
        restorationGuard: guard,
      );
      final api = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((_) async => http.Response('{}', 200)),
      );
      final session = SessionController(tokens, AuthService(api), api);
      final starting = session.startSession(
        user: const SessionUser(
          userId: 'admin',
          gymId: 'gym',
          name: 'Admin',
          email: 'admin@example.invalid',
          role: AppRole.admin,
        ),
        token: 'synthetic-late-token',
      );
      await delayedPlatform.writeStarted.future;
      final loggingOut = session.logout();
      expect(session.user, isNull);
      expect(session.status, SessionStatus.unauthenticated);
      delayedPlatform.releaseWrite.complete();
      await starting;
      await loggingOut;
      expect(session.status, SessionStatus.unauthenticated);
      expect(await guard.isRestorationBlocked(), isTrue);
      expect(
        await TokenStorage(
          const FlutterSecureStorage(),
          restorationGuard: FileSessionRestorationGuard.inDirectory(
            markerDirectory,
          ),
        ).readToken(),
        isNull,
      );
      expect(delayedPlatform.data['auth_token'], 'synthetic-late-token');
      session.dispose();
      api.close();
    },
  );
}

class RecordingSecureStoragePlatform extends TestFlutterSecureStoragePlatform {
  RecordingSecureStoragePlatform(super.data);

  Map<String, String> lastOptions = const {};
  bool failDeletes = false;
  bool failReads = false;
  int readCount = 0;

  @override
  Future<void> write({
    required String key,
    required String value,
    required Map<String, String> options,
  }) {
    lastOptions = options;
    return super.write(key: key, value: value, options: options);
  }

  @override
  Future<String?> read({
    required String key,
    required Map<String, String> options,
  }) {
    lastOptions = options;
    readCount++;
    if (failReads) throw StateError('Synthetic secure read failure');
    return super.read(key: key, options: options);
  }

  @override
  Future<void> delete({
    required String key,
    required Map<String, String> options,
  }) {
    lastOptions = options;
    if (failDeletes) throw StateError('Synthetic secure-store failure');
    return super.delete(key: key, options: options);
  }
}

class DelayedSecureStoragePlatform extends RecordingSecureStoragePlatform {
  DelayedSecureStoragePlatform(super.data);
  final writeStarted = Completer<void>();
  final releaseWrite = Completer<void>();
  @override
  Future<void> write({
    required String key,
    required String value,
    required Map<String, String> options,
  }) async {
    writeStarted.complete();
    await releaseWrite.future;
    await super.write(key: key, value: value, options: options);
  }
}
