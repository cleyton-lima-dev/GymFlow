import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/create_exercise_request.dart';
import 'package:http/http.dart' as http;

class CreateExerciseViewModel extends ChangeNotifier {
  CreateExerciseViewModel(this._service);

  final ExercisesService _service;

  bool _isSubmitting = false;
  String? _errorMessage;

  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;

  Future<bool> submit({
    required String name,
    required String muscleGroup,
    required String description,
  }) async {
    if (_isSubmitting) {
      return false;
    }

    final normalizedName = name.trim();
    final normalizedMuscleGroup = muscleGroup.trim();

    if (normalizedName.isEmpty) {
      _errorMessage = 'Informe o nome do exercício.';
      notifyListeners();
      return false;
    }

    if (normalizedMuscleGroup.isEmpty) {
      _errorMessage = 'Informe o grupo muscular.';
      notifyListeners();
      return false;
    }

    if (normalizedName.length > 150) {
      _errorMessage =
      'O nome deve ter no máximo 150 caracteres.';
      notifyListeners();
      return false;
    }

    if (normalizedMuscleGroup.length > 100) {
      _errorMessage =
      'O grupo muscular deve ter no máximo 100 caracteres.';
      notifyListeners();
      return false;
    }

    if (description.trim().length > 500) {
      _errorMessage =
      'A descrição deve ter no máximo 500 caracteres.';
      notifyListeners();
      return false;
    }

    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _service.createExercise(
        request: CreateExerciseRequest(
          name: normalizedName,
          muscleGroup: normalizedMuscleGroup,
          description: description,
        ),
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'O cadastro demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível cadastrar o exercício.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 409) {
      return 'Já existe um exercício com esses dados.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para cadastrar exercícios.';
    }

    if (exception.statusCode == 400) {
      return 'Verifique os dados informados e tente novamente.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de exercícios está temporariamente indisponível.';
    }

    return 'Não foi possível cadastrar o exercício.';
  }
}
