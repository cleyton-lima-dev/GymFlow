import 'package:flutter/foundation.dart';
import 'package:gymflow/app/session/session_status.dart';
import 'package:gymflow/core/storage/token_storage.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:gymflow/app/session/session_user.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/app/session/app_role.dart';

class SessionController extends ChangeNotifier {
  SessionController(
      this._tokenStorage,
      this._authService,
      );

  final TokenStorage _tokenStorage;
  final AuthService _authService;

  SessionStatus _status = SessionStatus.unknown;
  SessionUser? _user;

  SessionStatus get status => _status;
  SessionUser? get user => _user;

  Future<void> bootstrap() async {
    final token = await _tokenStorage.readToken();

    if (token == null) {
      _status = SessionStatus.unauthenticated;
      notifyListeners();
      return;
    }

    try {
      final currentUser = await _authService.getCurrentUser(token);

      _user = SessionUser(
        userId: currentUser.userId,
        name: currentUser.name,
        email: currentUser.email,
        role: AppRole.fromApiValue(currentUser.role),
      );

      _status = SessionStatus.authenticated;
      notifyListeners();
    } on ApiException catch (exception) {
      if (exception.statusCode == 401) {
        await _tokenStorage.deleteToken();

        _user = null;
        _status = SessionStatus.unauthenticated;
        notifyListeners();
        return;
      }

      rethrow;
    }
  }

  Future<void> login({
    required String email,
    required String password,
  }) async {
    final response = await _authService.login(
      email: email,
      password: password,
    );

    await startSession(
      user: SessionUser(
        userId: response.userId,
        name: response.name,
        email: response.email,
        role: AppRole.fromApiValue(response.role),
      ),
      token: response.token,
    );
  }

  Future<void> startSession({
    required SessionUser user,
    required String token,
  }) async {
    await _tokenStorage.saveToken(token);

    _user = user;
    _status = SessionStatus.authenticated;

    notifyListeners();
  }

  Future<void> logout() async {
    await _tokenStorage.deleteToken();

    _user = null;

    _status = SessionStatus.unauthenticated;

    notifyListeners();
  }
}
