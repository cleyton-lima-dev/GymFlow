import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:avelri_gestao/core/storage/session_restoration_guard.dart';

class TokenDeletionException implements Exception {
  const TokenDeletionException({
    required this.restorationBlocked,
    this.credentialDeleted = false,
  });

  /// Whether the independent logout intent was durably recorded.
  final bool restorationBlocked;
  final bool credentialDeleted;
}

class TokenStorage {
  TokenStorage(this._storage, {SessionRestorationGuard? restorationGuard})
    : _restorationGuard =
          restorationGuard ?? FileSessionRestorationGuard.applicationSupport();

  static const _tokenKey = 'auth_token';

  final FlutterSecureStorage _storage;
  final SessionRestorationGuard _restorationGuard;
  Future<void> _pendingOperation = Future<void>.value();

  // A logout queued during a credential write must be the last persistence
  // operation, so an older login cannot re-enable restoration afterward.
  Future<T> _serialize<T>(Future<T> Function() operation) {
    final result = _pendingOperation.then((_) => operation());
    _pendingOperation = result.then<void>((_) {}, onError: (Object _) {});
    return result;
  }

  Future<void> saveToken(String token) => _serialize(() async {
    await _restorationGuard.blockRestoration();
    await _storage.write(key: _tokenKey, value: token);
    // Only an explicitly completed login may remove the logout intent.
    await _restorationGuard.allowRestoration();
  });

  Future<String?> readToken() => _serialize(() async {
    if (await _restorationGuard.isRestorationBlocked()) return null;
    return _storage.read(key: _tokenKey);
  });

  Future<void> deleteToken() => _serialize(() async {
    var restorationBlocked = false;
    try {
      await _restorationGuard.blockRestoration();
      restorationBlocked = true;
    } catch (_) {
      // Still attempt secure deletion if the independent marker is unwritable.
      // A successful deletion also prevents restoration; dual failure must be
      // surfaced explicitly by the session UI, without logging credentials.
    }
    try {
      await _storage.delete(key: _tokenKey);
    } catch (_) {
      throw TokenDeletionException(restorationBlocked: restorationBlocked);
    }
    if (!restorationBlocked) {
      throw const TokenDeletionException(
        restorationBlocked: false,
        credentialDeleted: true,
      );
    }
  });
}
