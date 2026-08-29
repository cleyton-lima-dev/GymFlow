import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:http/http.dart' as http;

class StudentWorkoutDayViewModel extends ChangeNotifier {
  StudentWorkoutDayViewModel(this._service, WorkoutDayDetails day)
    : _workoutDayId = day.id,
      _completedToday = day.completedToday,
      _completedAt = day.completedToday ? day.lastCompletedAt : null,
      _completedAtUtcOffsetMinutes = day.completedToday
          ? day.lastCompletedAtUtcOffsetMinutes
          : null;

  final WorkoutsService _service;
  final String _workoutDayId;

  bool _isCompleting = false;
  bool _completedToday;
  DateTime? _completedAt;
  int? _completedAtUtcOffsetMinutes;
  String? _errorMessage;

  bool get isCompleting => _isCompleting;
  bool get completedToday => _completedToday;
  DateTime? get completedAt => _completedAt;
  int? get completedAtUtcOffsetMinutes => _completedAtUtcOffsetMinutes;
  String? get errorMessage => _errorMessage;

  bool get canComplete => !_isCompleting && !_completedToday;

  Future<bool> complete() async {
    if (!canComplete) {
      return false;
    }

    _isCompleting = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response = await _service.completeMyDay(
        workoutDayId: _workoutDayId,
      );

      _completedToday = true;
      _completedAt = response.completedAt;
      _completedAtUtcOffsetMinutes = response.completedAtUtcOffsetMinutes;

      return true;
    } on ApiException catch (exception) {
      if (exception.statusCode == 409) {
        final synchronized = await _synchronizeAfterConflict();

        if (synchronized) {
          return true;
        }
      }

      _errorMessage = _messageFromApiException(exception);

      return false;
    } on TimeoutException {
      _errorMessage =
          'A conclusão demorou mais que o esperado. '
          'Verifique sua conexão e tente novamente.';

      return false;
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';

      return false;
    } on FormatException {
      _errorMessage = 'O servidor retornou uma resposta inválida.';

      return false;
    } catch (_) {
      _errorMessage = 'Não foi possível concluir este dia.';

      return false;
    } finally {
      _isCompleting = false;
      notifyListeners();
    }
  }

  Future<bool> _synchronizeAfterConflict() async {
    try {
      final workout = await _service.getMyCurrentWorkout();

      if (workout == null) {
        return false;
      }

      for (final day in workout.days) {
        if (day.id != _workoutDayId) {
          continue;
        }

        if (!day.completedToday) {
          return false;
        }

        _completedToday = true;
        _completedAt = day.lastCompletedAt;
        _completedAtUtcOffsetMinutes = day.lastCompletedAtUtcOffsetMinutes;

        return true;
      }
    } catch (_) {
      return false;
    }

    return false;
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para concluir este dia.';
    }

    if (exception.statusCode == 400) {
      return 'Este dia não está disponível no seu treino ativo.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível concluir este dia no estado atual.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de treinos está temporariamente indisponível.';
    }

    return 'Não foi possível concluir este dia.';
  }
}
