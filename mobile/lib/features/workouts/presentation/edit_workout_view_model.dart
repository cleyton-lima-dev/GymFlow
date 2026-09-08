import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/update_workout_request.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/models/workout_draft.dart';
import 'package:gymflow/features/workouts/models/workout_draft_from_details.dart';
import 'package:http/http.dart' as http;

class EditWorkoutViewModel extends ChangeNotifier {
  EditWorkoutViewModel(
      this._service,
      WorkoutDetails workout,
      )   : _workoutId = workout.id,
        _name = workout.name,
        _description = workout.description ?? '' {
    _days.addAll(
      workout.toWorkoutDraftDays(),
    );
  }

  final WorkoutsService _service;
  final String _workoutId;

  String _name;
  String _description;

  final List<WorkoutDraftDay> _days = [];

  bool _isSubmitting = false;
  String? _errorMessage;

  String get name => _name;
  String get description => _description;

  List<WorkoutDraftDay> get days =>
      List.unmodifiable(_days);

  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;

  void setName(String value) {
    _name = value;
    _clearError();
  }

  void setDescription(String value) {
    _description = value;
  }

  void addDay(WorkoutDraftDay day) {
    _days.add(day);
    _clearError();
    notifyListeners();
  }

  void replaceDay(
      int index,
      WorkoutDraftDay day,
      ) {
    if (!_isValidIndex(index)) {
      return;
    }

    _days[index] = day;
    notifyListeners();
  }

  void removeDay(int index) {
    if (!_isValidIndex(index)) {
      return;
    }

    _days.removeAt(index);
    notifyListeners();
  }

  void reorderDay(
      int oldIndex,
      int newIndex,
      ) {
    if (!_isValidIndex(oldIndex) ||
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
      'Informe o nome do treino.';
      notifyListeners();
      return false;
    }

    if (_days.isEmpty) {
      _errorMessage =
      'O treino deve possuir pelo menos uma divisão.';
      notifyListeners();
      return false;
    }

    for (final day in _days) {
      if (day.name.trim().isEmpty) {
        _errorMessage =
        'Todas as divisões precisam ter um nome.';
        notifyListeners();
        return false;
      }
    }

    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final request = UpdateWorkoutRequest(
        name: normalizedName,
        description: _nullableTrimmed(
          _description,
        ),
        days: _days
            .asMap()
            .entries
            .map(
              (entry) =>
              entry.value.toUpdateRequest(
                order: entry.key + 1,
              ),
        )
            .toList(growable: false),
      );

      await _service.updateWorkout(
        workoutId: _workoutId,
        request: request,
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'O servidor demorou mais que o esperado para atualizar o treino.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível atualizar o treino.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  bool _isValidIndex(int index) {
    return index >= 0 &&
        index < _days.length;
  }

  void _clearError() {
    if (_errorMessage == null) {
      return;
    }

    _errorMessage = null;
    notifyListeners();
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 400) {
      return 'Revise os dados do treino.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para editar este treino.';
    }

    if (exception.statusCode == 404) {
      return 'Treino ou exercício não encontrado.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível atualizar este treino no estado atual.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de treinos está temporariamente indisponível.';
    }

    return 'Não foi possível atualizar o treino.';
  }
}

String? _nullableTrimmed(String value) {
  final normalized = value.trim();

  if (normalized.isEmpty) {
    return null;
  }

  return normalized;
}
