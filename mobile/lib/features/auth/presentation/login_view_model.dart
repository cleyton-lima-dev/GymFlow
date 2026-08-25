import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:http/http.dart' as http;

class LoginViewModel extends ChangeNotifier {
  LoginViewModel(this._sessionController);

  final SessionController _sessionController;

  bool _isLoading = false;
  String? _errorMessage;

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  Future<void> login({
    required String email,
    required String password,
  }) async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _sessionController.login(
        email: email.trim(),
        password: password,
      );
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A conexão demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      _errorMessage =
      'O servidor retornou uma resposta inválida. Tente novamente.';
    } catch (_) {
      _errorMessage =
      'Não foi possível entrar. Tente novamente.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void clearError() {
    if (_errorMessage == null) {
      return;
    }

    _errorMessage = null;
    notifyListeners();
  }

  String _messageFromApiException(ApiException exception) {
    final apiMessage = _extractApiMessage(exception.message);

    if (exception.statusCode == 401) {
      return apiMessage ?? 'E-mail ou senha inválidos.';
    }

    if (exception.statusCode >= 500) {
      return 'O GymFlow está temporariamente indisponível. Tente novamente.';
    }

    return apiMessage ?? 'Não foi possível entrar. Tente novamente.';
  }

  String? _extractApiMessage(String responseBody) {
    try {
      final decoded = jsonDecode(responseBody);

      if (decoded is Map<String, dynamic>) {
        final message = decoded['message'];

        if (message is String && message.trim().isNotEmpty) {
          return message.trim();
        }
      }
    } catch (_) {
      return null;
    }

    return null;
  }
}
