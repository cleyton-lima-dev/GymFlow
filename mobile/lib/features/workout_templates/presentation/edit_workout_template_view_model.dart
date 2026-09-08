import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/create_workout_template_request.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_draft.dart';
import 'package:http/http.dart' as http;

class EditWorkoutTemplateViewModel extends ChangeNotifier {
  EditWorkoutTemplateViewModel(
      this._service,
      this._templateId,
      );

  final WorkoutTemplatesService _service;
  final String _templateId;

  String _name = '';
  String _description = '';
  bool _isActive = true;
  bool _initialIsActive = true;

  final List<WorkoutTemplateDraftDay> _days = [];

  bool _isLoading = false;
  bool _isSubmitting = false;
  bool _hasLoaded = false;

  String? _errorMessage;

  String get name => _name;
  String get description => _description;
  bool get isActive => _isActive;

  List<WorkoutTemplateDraftDay> get days =>
      List.unmodifiable(_days);

  bool get isLoading => _isLoading;
  bool get isSubmitting => _isSubmitting;

  bool get hasLoaded => _hasLoaded;

  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading || _hasLoaded) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final template = await _service.getById(
        _templateId,
      );

      _name = template.name;
      _description = template.description ?? '';

      _isActive = template.isActive;
      _initialIsActive = template.isActive;

      final sortedDays = [...template.days]
        ..sort(
              (a, b) => a.order.compareTo(b.order),
        );

      _days
        ..clear()
        ..addAll(
          sortedDays.map(
                (day) {
              final sortedExercises = [...day.exercises]
                ..sort(
                      (a, b) => a.order.compareTo(b.order),
                );

              return WorkoutTemplateDraftDay(
                name: day.name,
                exercises: sortedExercises
                    .map(
                      (exercise) =>
                      WorkoutTemplateDraftExercise(
                        exerciseId:
                        exercise.exerciseId,
                        exerciseName:
                        exercise.exerciseName,
                        muscleGroup:
                        exercise.muscleGroup,
                        sets: exercise.sets,
                        repetitions:
                        exercise.repetitions,
                        restSeconds:
                        exercise.restSeconds,
                        notes: exercise.notes,
                      ),
                )
                    .toList(growable: false),
              );
            },
          ),
        );

      _hasLoaded = true;
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

  Future<void> retryLoad() async {
    _hasLoaded = false;
    await load();
  }

  void setName(String value) {
    _name = value;
    _clearError();
  }

  void setDescription(String value) {
    _description = value;
  }

  void setIsActive(bool value) {
    if (_isActive == value) {
      return;
    }

    _isActive = value;
    _clearError();
    notifyListeners();
  }

  void addDay(
      WorkoutTemplateDraftDay day,
      ) {
    _days.add(day);
    _clearError();
    notifyListeners();
  }

  void replaceDay(
      int dayIndex,
      WorkoutTemplateDraftDay day,
      ) {
    if (!_isValidDayIndex(dayIndex)) {
      return;
    }

    _days[dayIndex] = day;
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

  void reorderDay(
      int oldIndex,
      int newIndex,
      ) {
    if (!_isValidDayIndex(oldIndex) ||
        newIndex < 0 ||
        newIndex >= _days.length) {
      return;
    }

    final day = _days.removeAt(oldIndex);
    _days.insert(newIndex, day);

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

      await _service.update(
        _templateId,
        request,
      );

      if (_isActive != _initialIsActive) {
        await _service.updateStatus(
          templateId: _templateId,
          isActive: _isActive,
        );

        _initialIsActive = _isActive;
      }

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
      'Não foi possível atualizar o modelo de treino.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  void clearError() {
    _clearError();
  }

  void _clearError() {
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

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 400) {
      return 'Revise os dados do modelo de treino.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para editar este modelo.';
    }

    if (exception.statusCode == 404) {
      return 'Modelo de treino não encontrado.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível salvar o modelo devido a um conflito nos dados.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de modelos está temporariamente indisponível.';
    }

    return 'Não foi possível atualizar o modelo de treino.';
  }
}

String? _nullableTrimmed(String value) {
  final normalized = value.trim();

  if (normalized.isEmpty) {
    return null;
  }

  return normalized;
}
