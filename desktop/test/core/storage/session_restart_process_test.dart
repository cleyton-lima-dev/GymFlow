import 'dart:io';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/session_restoration_guard.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

/// Run the two named tests in separate `flutter test` invocations with the same
/// explicit synthetic directory to verify persistence across OS processes.
/// Windows secure storage remains mocked in both processes.
const _externalDirectory = String.fromEnvironment(
  'GYMFLOW_SESSION_GUARD_TEST_DIRECTORY',
);

void main() {
  late FlutterSecureStoragePlatform original;
  late Directory directory;
  setUp(() async {
    original = FlutterSecureStoragePlatform.instance;
    directory = _externalDirectory.isEmpty
        ? await Directory.systemTemp.createTemp('avelri-process-guard-test-')
        : Directory(_externalDirectory);
    await directory.create(recursive: true);
    FlutterSecureStoragePlatform.instance = _DeleteFailsPlatform({
      'auth_token': 'synthetic-stale-token',
    });
  });
  tearDown(() async {
    FlutterSecureStoragePlatform.instance = original;
    if (_externalDirectory.isEmpty) await directory.delete(recursive: true);
  });

  test('persist logout phase', () async {
    final tokens = TokenStorage(
      const FlutterSecureStorage(),
      restorationGuard: FileSessionRestorationGuard.inDirectory(directory),
    );
    await expectLater(
      tokens.deleteToken(),
      throwsA(
        isA<TokenDeletionException>().having(
          (error) => error.restorationBlocked,
          'durable guard',
          isTrue,
        ),
      ),
    );
    expect(
      await FileSessionRestorationGuard.inDirectory(directory)
          .isRestorationBlocked(),
      isTrue,
    );
  });

  test('restore after restart phase', () async {
    if (_externalDirectory.isEmpty) {
      await FileSessionRestorationGuard.inDirectory(directory)
          .blockRestoration();
    }
    final guard = FileSessionRestorationGuard.inDirectory(directory);
    expect(
      await guard.isRestorationBlocked(),
      isTrue,
      reason: 'The first process must have recorded its logout before this process starts.',
    );
    var requests = 0;
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((_) async {
        requests++;
        return http.Response('{}', 200);
      }),
    );
    final session = SessionController(
      TokenStorage(const FlutterSecureStorage(), restorationGuard: guard),
      AuthService(api),
      api,
    );
    await session.bootstrap();
    expect(session.status, SessionStatus.unauthenticated);
    expect(session.user, isNull);
    expect(requests, 0);
    session.dispose();
    api.close();
  });
}

class _DeleteFailsPlatform extends TestFlutterSecureStoragePlatform {
  _DeleteFailsPlatform(super.data);
  @override
  Future<void> delete({
    required String key,
    required Map<String, String> options,
  }) async {
    throw StateError('Synthetic secure deletion failure');
  }
}
