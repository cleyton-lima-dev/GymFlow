import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/create_workout_request.dart';
import 'package:gymflow/features/workouts/models/workout_draft.dart';
import 'package:http/http.dart' as http;
import 'package:gymflow/features/workouts/models/create_workout_from_template_request.dart';

class CreateWorkoutViewModel extends ChangeNotifier {
  CreateWorkoutViewModel(
      this._service,
      this._studentId, {
        this._sourceTemplateId,
        String initialName = '',
        String initialDescription = '',
        List<WorkoutDraftDay>? initialDays,
      })  : _name = initialName,
        _description = initialDescription {
    if (initialDays != null) {
      _days.addAll(initialDays);
    }
  }

  final WorkoutsService _service;
  final String _studentId;
  final String? _sourceTemplateId;

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

  void addDay(
      WorkoutDraftDay day,
      ) {
    _days.add(day);
    _clearError();
    notifyListeners();
  }

  void replaceDay(
      int dayIndex,
      WorkoutDraftDay day,
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
      'Informe o nome do treino.';
      notifyListeners();
      return false;
    }

    for (final day in _days) {
      if (day.name.trim().isEmpty) {
        _errorMessage =
        'Todas as divisões do treino precisam ter um nome.';
        notifyListeners();
        return false;
      }
    }

    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final days = _days
          .asMap()
          .entries
          .map(
            (entry) => entry.value.toRequest(
          order: entry.key + 1,
        ),
      )
          .toList(growable: false);

      final description = _nullableTrimmed(
        _description,
      );

      if (_sourceTemplateId == null) {
        await _service.createWorkout(
          request: CreateWorkoutRequest(
            studentId: _studentId,
            name: normalizedName,
            description: description,
            days: days,
          ),
        );
      } else {
        await _service.createFromTemplate(
          request: CreateWorkoutFromTemplateRequest(
            studentId: _studentId,
            templateId: _sourceTemplateId,
            name: normalizedName,
            description: description,
            days: days,
          ),
        );
      }

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'O servidor demorou mais que o esperado para criar o treino.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível criar o treino.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  void clearError() {
    _clearError();
  }

  bool _isValidDayIndex(int dayIndex) {
    return dayIndex >= 0 &&
        dayIndex < _days.length;
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
      return 'Você não possui permissão para criar este treino.';
    }

    if (exception.statusCode == 404) {
      return 'Aluno ou exercício não encontrado.';
    }

    if (exception.statusCode == 409) {
      return 'Este aluno já possui um treino ativo.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de treinos está temporariamente indisponível.';
    }

    return 'Não foi possível criar o treino.';
  }
}

String? _nullableTrimmed(String value) {
  final normalized = value.trim();

  if (normalized.isEmpty) {
    return null;
  }

  return normalized;
}
