import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/create_workout_template_request.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_draft.dart';
import 'package:http/http.dart' as http;

class CreateWorkoutTemplateViewModel extends ChangeNotifier {
  CreateWorkoutTemplateViewModel(
      this._service,
      );

  final WorkoutTemplatesService _service;

  String _name = '';
  String _description = '';

  final List<WorkoutTemplateDraftDay> _days = [];

  bool _isSubmitting = false;
  String? _errorMessage;

  String get name => _name;
  String get description => _description;

  List<WorkoutTemplateDraftDay> get days =>
      List.unmodifiable(_days);

  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;

  void setName(String value) {
    _name = value;

    if (_errorMessage != null) {
      _errorMessage = null;
      notifyListeners();
    }
  }

  void setDescription(String value) {
    _description = value;
  }

  void addDay(
      WorkoutTemplateDraftDay day,
      ) {
    _days.add(day);
    _errorMessage = null;
    notifyListeners();
  }

  void renameDay(
      int dayIndex,
      String name,
      ) {
    if (!_isValidDayIndex(dayIndex)) {
      return;
    }

    _days[dayIndex].name = name;
    notifyListeners();
  }

  void removeDay(
      int dayIndex,
      ) {
    if (!_isValidDayIndex(dayIndex)) {
      return;
    }

    _days.removeAt(dayIndex);
    notifyListeners();
  }

  void addExercise(
      int dayIndex,
      WorkoutTemplateDraftExercise exercise,
      ) {
    if (!_isValidDayIndex(dayIndex)) {
      return;
    }

    _days[dayIndex].exercises.add(exercise);
    notifyListeners();
  }

  void updateExercise(
      int dayIndex,
      int exerciseIndex,
      WorkoutTemplateDraftExercise exercise,
      ) {
    if (!_isValidExerciseIndex(
      dayIndex,
      exerciseIndex,
    )) {
      return;
    }

    _days[dayIndex].exercises[exerciseIndex] =
        exercise;

    notifyListeners();
  }

  void removeExercise(
      int dayIndex,
      int exerciseIndex,
      ) {
    if (!_isValidExerciseIndex(
      dayIndex,
      exerciseIndex,
    )) {
      return;
    }

    _days[dayIndex].exercises.removeAt(
      exerciseIndex,
    );

    notifyListeners();
  }

  Future<bool> submit() async {
    if (_isSubmitting) {
      return false;
    }

    final normalizedName = _name.trim();

    if (normalizedName.isEmpty) {
      _errorMessage =
      'Informe o nome do modelo de treino.';
      notifyListeners();
      return false;
    }

    for (final day in _days) {
      if (day.name.trim().isEmpty) {
        _errorMessage =
        'Todos os dias do modelo precisam ter um nome.';
        notifyListeners();
        return false;
      }
    }

    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final request = CreateWorkoutTemplateRequest(
        name: normalizedName,
        description: _nullableTrimmed(
          _description,
        ),
        days: _days
            .asMap()
            .entries
            .map(
              (entry) => entry.value.toRequest(
            order: entry.key + 1,
          ),
        )
            .toList(growable: false),
      );

      await _service.create(request);
      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'O servidor demorou mais que o esperado para salvar o modelo.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar a resposta do servidor.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível criar o modelo de treino.';
      return false;
    } finally {
      _isSubmitting = false;
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

  bool _isValidDayIndex(int dayIndex) {
    return dayIndex >= 0 &&
        dayIndex < _days.length;
  }

  bool _isValidExerciseIndex(
      int dayIndex,
      int exerciseIndex,
      ) {
    if (!_isValidDayIndex(dayIndex)) {
      return false;
    }

    return exerciseIndex >= 0 &&
        exerciseIndex <
            _days[dayIndex].exercises.length;
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 400) {
      return 'Revise os dados do modelo de treino.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para criar modelos.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível salvar o modelo devido a um conflito nos dados.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de modelos está temporariamente indisponível.';
    }

    return 'Não foi possível criar o modelo de treino.';
  }
}

String? _nullableTrimmed(String value) {
  final normalized = value.trim();

  if (normalized.isEmpty) {
    return null;
  }

  return normalized;
}
