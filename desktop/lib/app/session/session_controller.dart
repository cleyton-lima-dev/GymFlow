import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';

class ManagementAccessDeniedException implements Exception {
  const ManagementAccessDeniedException();
}

class SessionController extends ChangeNotifier {
  SessionController(this._tokenStorage, this._authService, this._apiClient);

  final TokenStorage _tokenStorage;
  final AuthService _authService;
  final ApiClient _apiClient;

  SessionStatus _status = SessionStatus.unknown;
  SessionUser? _user;
  int _operationGeneration = 0;
  int _revision = 0;
  String? _persistenceWarning;
  Future<void>? _pendingBootstrap;

  SessionStatus get status => _status;
  SessionUser? get user => _user;
  int get revision => _revision;
  String? get persistenceWarning => _persistenceWarning;

  Future<void> bootstrap() {
    if (_status != SessionStatus.unknown) return Future<void>.value();
    return _pendingBootstrap ??= _bootstrap().whenComplete(() {
      _pendingBootstrap = null;
    });
  }

  Future<void> _bootstrap() async {
    final generation = ++_operationGeneration;
    _apiClient.clearAccessToken();
    try {
      final token = await _tokenStorage.readToken();
      if (generation != _operationGeneration) return;
      if (token == null || token.isEmpty) {
        _clearMemory();
        return;
      }

      _apiClient.setAccessToken(token);
      final currentUser = await _authService.getCurrentUser();
      if (generation != _operationGeneration) return;

      final role = AppRole.fromApiValue(currentUser.role);
      if (role != AppRole.admin || currentUser.gymId.isEmpty) {
        await invalidateSession();
        return;
      }

      _user = SessionUser(
        userId: currentUser.userId,
        gymId: currentUser.gymId,
        name: currentUser.name,
        email: currentUser.email,
        role: role,
      );

      _status = SessionStatus.authenticated;
      _revision++;
      notifyListeners();
    } on ApiException catch (exception) {
      if (generation != _operationGeneration) return;
      if (exception.statusCode == 401) {
        await invalidateSession();
        return;
      }

      _apiClient.clearAccessToken();
      rethrow;
    } catch (_) {
      if (generation != _operationGeneration) return;
      _apiClient.clearAccessToken();
      rethrow;
    }
  }

  Future<void> login({required String email, required String password}) async {
    final generation = ++_operationGeneration;
    _clearMemory();
    await _persistInvalidation(generation);
    if (generation != _operationGeneration) return;
    final response = await _authService.login(email: email, password: password);
    if (generation != _operationGeneration) return;

    final role = AppRole.fromApiValue(response.role);

    if (role != AppRole.admin || response.gymId.isEmpty) {
      throw const ManagementAccessDeniedException();
    }

    await _persistAndStartSession(
      generation: generation,
      user: SessionUser(
        userId: response.userId,
        gymId: response.gymId,
        name: response.name,
        email: response.email,
        role: role,
      ),
      token: response.token,
    );
  }

  Future<void> startSession({
    required SessionUser user,
    required String token,
  }) async {
    final generation = ++_operationGeneration;
    _clearMemory();
    if (user.role != AppRole.admin || user.gymId.isEmpty) {
      throw const ManagementAccessDeniedException();
    }
    await _persistAndStartSession(
      user: user,
      token: token,
      generation: generation,
    );
  }

  Future<void> _persistAndStartSession({
    required SessionUser user,
    required String token,
    required int generation,
  }) async {
    if (token.isEmpty) throw const FormatException('Empty access token.');
    await _tokenStorage.saveToken(token);
    if (generation != _operationGeneration) return;
    _apiClient.setAccessToken(token);

    _user = user;
    _status = SessionStatus.authenticated;
    _persistenceWarning = null;
    _revision++;

    notifyListeners();
  }

  Future<void> invalidateSession() async {
    final generation = ++_operationGeneration;
    _clearMemory();
    await _persistInvalidation(generation);
  }

  Future<void> _persistInvalidation(int generation) async {
    try {
      await _tokenStorage.deleteToken();
      if (generation == _operationGeneration && _persistenceWarning != null) {
        _persistenceWarning = null;
        notifyListeners();
      }
    } on TokenDeletionException catch (exception) {
      if (generation != _operationGeneration) return;
      _persistenceWarning = exception.credentialDeleted
          ? 'A credencial foi apagada, mas não foi possível registrar o bloqueio de restauração. Tente encerrar a sessão novamente.'
          : exception.restorationBlocked
          ? 'A sessão foi encerrada. A credencial não pôde ser apagada, mas a restauração automática está bloqueada.'
          : 'A sessão foi encerrada neste processo, mas não foi possível impedir a restauração no próximo início. Tente sair novamente antes de fechar o aplicativo.';
      notifyListeners();
    }
  }

  void _clearMemory() {
    _apiClient.clearAccessToken();
    final shouldNotify =
        _user != null || _status != SessionStatus.unauthenticated;

    _user = null;
    _status = SessionStatus.unauthenticated;
    _revision++;

    if (shouldNotify) {
      notifyListeners();
    }
  }

  Future<void> logout() {
    return invalidateSession();
  }
}
