import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class AccessControlViewModel extends ChangeNotifier {
  AccessControlViewModel(this._accessAgentsService);

  final AccessAgentsService _accessAgentsService;

  final List<AccessAgentSummary> _agents = [];

  bool _isLoading = false;
  bool _isDisposed = false;
  String? _errorMessage;

  List<AccessAgentSummary> get agents => List.unmodifiable(_agents);

  bool get isLoading => _isLoading;

  bool get hasAgents => _agents.isNotEmpty;

  String? get errorMessage => _errorMessage;

  Future<void> loadInitial() {
    return _load(showLoading: true);
  }

  Future<void> retry() {
    return _load(showLoading: true);
  }

  Future<void> refresh() {
    return _load(showLoading: false);
  }

  Future<void> _load({required bool showLoading}) async {
    if (showLoading) {
      _isLoading = true;
    }

    _errorMessage = null;
    _notifySafely();

    try {
      final agents = await _accessAgentsService.getAgents();

      if (_isDisposed) {
        return;
      }

      _agents
        ..clear()
        ..addAll(agents);
    } on ApiException catch (exception) {
      if (_isDisposed) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
          'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
          'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
          'O status do controle de acesso retornou em formato inválido.';
    } catch (_) {
      if (_isDisposed) {
        return;
      }

      _errorMessage = 'Não foi possível carregar o controle de acesso.';
    } finally {
      if (!_isDisposed) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar o controle de acesso.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar o controle de acesso.';
  }

  void _notifySafely() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    super.dispose();
  }
}
