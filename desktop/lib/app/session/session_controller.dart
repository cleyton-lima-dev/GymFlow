import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';

class SessionController extends ChangeNotifier {
  SessionController(
      this._tokenStorage,
      this._authService,
      this._apiClient,
      );

  final TokenStorage _tokenStorage;
  final AuthService _authService;
  final ApiClient _apiClient;

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

    _apiClient.setAccessToken(token);

    try {
      final currentUser = await _authService.getCurrentUser();

      _user = SessionUser(
        userId: currentUser.userId,
        gymId: currentUser.gymId,
        name: currentUser.name,
        email: currentUser.email,
        role: AppRole.fromApiValue(currentUser.role),
      );

      _status = SessionStatus.authenticated;
      notifyListeners();
    } on ApiException catch (exception) {
      if (exception.statusCode == 401) {
        await invalidateSession();
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
        gymId: response.gymId,
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
    _apiClient.setAccessToken(token);

    _user = user;
    _status = SessionStatus.authenticated;

    notifyListeners();
  }

  Future<void> invalidateSession() async {
    _apiClient.clearAccessToken();
    await _tokenStorage.deleteToken();

    final shouldNotify =
        _user != null || _status != SessionStatus.unauthenticated;

    _user = null;
    _status = SessionStatus.unauthenticated;

    if (shouldNotify) {
      notifyListeners();
    }
  }

  Future<void> logout() {
    return invalidateSession();
  }
}
