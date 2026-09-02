import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:gymflow/features/auth/data/professor_response.dart';
import 'package:http/http.dart' as http;

class ProfessorsViewModel extends ChangeNotifier {
  ProfessorsViewModel(this._authService);

  final AuthService _authService;

  bool _isLoading = false;
  String? _errorMessage;
  List<ProfessorResponse> _professors = const [];

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  List<ProfessorResponse> get professors => _professors;

  Future<void> loadProfessors() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _professors = await _authService.getProfessors();
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A operação demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      _errorMessage =
      'O servidor retornou uma resposta inválida. Tente novamente.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar os professores. Tente novamente.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para visualizar professores.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar os professores.';
  }
}
