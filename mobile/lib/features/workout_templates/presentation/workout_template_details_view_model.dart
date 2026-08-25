import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';
import 'package:http/http.dart' as http;

class WorkoutTemplateDetailsViewModel extends ChangeNotifier {
  WorkoutTemplateDetailsViewModel(
      this._service,
      this._templateId,
      );

  final WorkoutTemplatesService _service;
  final String _templateId;

  WorkoutTemplateDetails? _template;
  bool _isLoading = false;
  String? _errorMessage;

  WorkoutTemplateDetails? get template => _template;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _template = await _service.getById(
        _templateId,
      );
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'O modelo demorou mais que o esperado para carregar.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os dados do modelo.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar o modelo de treino.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> refresh() async {
    await load();
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 404) {
      return 'Modelo de treino não encontrado.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar este modelo.';
    }

    if (exception.statusCode >= 500) {
      return 'O modelo de treino está temporariamente indisponível.';
    }

    return 'Não foi possível carregar o modelo de treino.';
  }
}
