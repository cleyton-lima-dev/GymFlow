import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/workout_history_item.dart';
import 'package:http/http.dart' as http;

class WorkoutHistoryViewModel extends ChangeNotifier {
  WorkoutHistoryViewModel(
      this._service,
      this._studentId,
      );

  final WorkoutsService _service;
  final String _studentId;

  final List<WorkoutHistoryItem> _items = [];

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  bool _isLoading = false;
  bool _isLoadingMore = false;
  bool _hasLoaded = false;

  String? _errorMessage;

  List<WorkoutHistoryItem> get items =>
      List.unmodifiable(_items);

  int get totalCount => _totalCount;

  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;
  bool get hasLoaded => _hasLoaded;

  bool get isEmpty =>
      _hasLoaded &&
          !_isLoading &&
          _items.isEmpty &&
          _errorMessage == null;

  bool get hasNextPage => _page < _totalPages;

  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response =
      await _service.getStudentHistory(
        studentId: _studentId,
        page: 1,
        pageSize: 20,
      );

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
      _hasLoaded = true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
      _hasLoaded = true;
    } on TimeoutException {
      _errorMessage =
      'O servidor demorou mais que o esperado para carregar o histórico.';
      _hasLoaded = true;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
      _hasLoaded = true;
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar o histórico de treinos.';
      _hasLoaded = true;
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar o histórico de treinos.';
      _hasLoaded = true;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> refresh() async {
    await load();
  }

  Future<void> loadMore() async {
    if (_isLoading ||
        _isLoadingMore ||
        !hasNextPage) {
      return;
    }

    _isLoadingMore = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response =
      await _service.getStudentHistory(
        studentId: _studentId,
        page: _page + 1,
        pageSize: 20,
      );

      _items.addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'O servidor demorou mais que o esperado para carregar mais registros.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar o histórico de treinos.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar mais registros.';
    } finally {
      _isLoadingMore = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 400) {
      return 'Não foi possível carregar o histórico deste aluno.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para visualizar este histórico.';
    }

    if (exception.statusCode == 404) {
      return 'Aluno não encontrado.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de histórico está temporariamente indisponível.';
    }

    return 'Não foi possível carregar o histórico de treinos.';
  }
}
