import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:gymflow/features/exercises/models/update_exercise_request.dart';
import 'package:http/http.dart' as http;

class EditExerciseViewModel extends ChangeNotifier {
  EditExerciseViewModel(
      this._service,
      this.exercise,
      ) : _isActive = exercise.isActive;

  final ExercisesService _service;
  final ExerciseSummary exercise;

  bool _isSaving = false;
  bool _isUpdatingStatus = false;
  bool _isActive;

  String? _errorMessage;

  bool get isSaving => _isSaving;
  bool get isUpdatingStatus => _isUpdatingStatus;
  bool get isActive => _isActive;

  bool get isBusy => _isSaving || _isUpdatingStatus;

  String? get errorMessage => _errorMessage;

  Future<bool> save({
    required String name,
    required String muscleGroup,
    required String description,
  }) async {
    if (isBusy) {
      return false;
    }

    final normalizedName = name.trim();
    final normalizedMuscleGroup = muscleGroup.trim();
    final normalizedDescription = description.trim();

    if (normalizedName.isEmpty) {
      _setError('Informe o nome do exercício.');
      return false;
    }

    if (normalizedMuscleGroup.isEmpty) {
      _setError('Informe o grupo muscular.');
      return false;
    }

    if (normalizedName.length > 150) {
      _setError(
        'O nome deve ter no máximo 150 caracteres.',
      );
      return false;
    }

    if (normalizedMuscleGroup.length > 100) {
      _setError(
        'O grupo muscular deve ter no máximo 100 caracteres.',
      );
      return false;
    }

    if (normalizedDescription.length > 500) {
      _setError(
        'A descrição deve ter no máximo 500 caracteres.',
      );
      return false;
    }

    _isSaving = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _service.updateExercise(
        exerciseId: exercise.id,
        request: UpdateExerciseRequest(
          name: normalizedName,
          muscleGroup: normalizedMuscleGroup,
          description: normalizedDescription,
        ),
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'A alteração demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível atualizar o exercício.';
      return false;
    } finally {
      _isSaving = false;
      notifyListeners();
    }
  }

  Future<bool> updateStatus(
      bool isActive,
      ) async {
    if (isBusy || _isActive == isActive) {
      return false;
    }

    _isUpdatingStatus = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _service.updateExerciseStatus(
        exerciseId: exercise.id,
        isActive: isActive,
      );

      _isActive = isActive;
      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromStatusApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'A alteração de status demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível alterar o status do exercício.';
      return false;
    } finally {
      _isUpdatingStatus = false;
      notifyListeners();
    }
  }

  void _setError(String message) {
    _errorMessage = message;
    notifyListeners();
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 404) {
      return 'Exercício não encontrado.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para editar exercícios.';
    }

    if (exception.statusCode == 400) {
      return 'Verifique os dados informados e tente novamente.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível salvar porque os dados '
          'entram em conflito com outro exercício.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de exercícios está '
          'temporariamente indisponível.';
    }

    return 'Não foi possível atualizar o exercício.';
  }

  String _messageFromStatusApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para alterar '
          'o status dos exercícios.';
    }

    if (exception.statusCode == 404) {
      return 'Exercício não encontrado.';
    }

    if (exception.statusCode >= 500) {
      return 'Não foi possível alterar o status agora.';
    }

    return 'Não foi possível alterar o status do exercício.';
  }
}
