import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:http/http.dart' as http;

class CreateProfessorViewModel extends ChangeNotifier {
  CreateProfessorViewModel(this._authService);

  final AuthService _authService;

  bool _isLoading = false;
  String? _errorMessage;

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  Future<bool> createProfessor({
    required String name,
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    if (_isLoading) {
      return false;
    }

    if (password != confirmPassword) {
      _errorMessage = 'As senhas não coincidem.';
      notifyListeners();
      return false;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _authService.registerProfessor(
        name: name,
        email: email,
        password: password,
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'A operação demorou mais que o esperado. Tente novamente.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
      return false;
    } on FormatException {
      _errorMessage =
      'O servidor retornou uma resposta inválida. Tente novamente.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível cadastrar o professor. Tente novamente.';
      return false;
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

    if (exception.statusCode == 400) {
      return apiMessage ?? 'Verifique os dados informados.';
    }

    if (exception.statusCode == 409) {
      return apiMessage ?? 'Já existe um usuário com este e-mail.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para cadastrar professores.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return apiMessage ?? 'Não foi possível cadastrar o professor.';
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
