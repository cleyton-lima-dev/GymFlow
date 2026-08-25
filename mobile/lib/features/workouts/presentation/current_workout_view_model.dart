import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:http/http.dart' as http;

class CurrentWorkoutViewModel extends ChangeNotifier {
  CurrentWorkoutViewModel(
      this._service,
      this._studentId,
      );

  final WorkoutsService _service;
  final String _studentId;

  WorkoutDetails? _workout;

  bool _isLoading = false;
  bool _hasLoaded = false;

  String? _errorMessage;

  WorkoutDetails? get workout => _workout;

  bool get isLoading => _isLoading;
  bool get hasLoaded => _hasLoaded;

  bool get hasWorkout => _workout != null;

  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _workout = await _service.getCurrentWorkout(
        studentId: _studentId,
      );

      _hasLoaded = true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'O treino demorou mais que o esperado para carregar.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os dados do treino.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar o treino atual.';
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
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar este treino.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de treinos está temporariamente indisponível.';
    }

    return 'Não foi possível carregar o treino atual.';
  }
}
