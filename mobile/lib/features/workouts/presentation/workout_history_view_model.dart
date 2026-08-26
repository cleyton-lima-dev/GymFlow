import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/paged_workout_history_response.dart';
import 'package:gymflow/features/workouts/models/workout_history_item.dart';
import 'package:http/http.dart' as http;

class WorkoutHistoryViewModel extends ChangeNotifier {
  WorkoutHistoryViewModel(this._service, String studentId)
    : _studentId = studentId;

  WorkoutHistoryViewModel.forCurrentUser(this._service) : _studentId = null;

  final WorkoutsService _service;
  final String? _studentId;

  final List<WorkoutHistoryItem> _items = [];

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  bool _isLoading = false;
  bool _isLoadingMore = false;
  bool _hasLoaded = false;

  String? _errorMessage;

  List<WorkoutHistoryItem> get items => List.unmodifiable(_items);

  int get page => _page;
  int get totalPages => _totalPages;
  int get totalCount => _totalCount;

  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;
  bool get hasLoaded => _hasLoaded;

  bool get isEmpty =>
      _hasLoaded && !_isLoading && _items.isEmpty && _errorMessage == null;

  bool get hasNextPage => _page < _totalPages;
  bool get hasPreviousPage => _page > 1;

  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response = await _fetchPage(1);

      _replaceItems(response);
      _hasLoaded = true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      _hasLoaded = true;
    } on TimeoutException {
      _errorMessage =
          'O servidor demorou mais que o esperado para carregar o histórico.';
      _hasLoaded = true;
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';
      _hasLoaded = true;
    } on FormatException {
      _errorMessage = 'Não foi possível interpretar o histórico de treinos.';
      _hasLoaded = true;
    } catch (_) {
      _errorMessage = 'Não foi possível carregar o histórico de treinos.';
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
    if (_isLoading || _isLoadingMore || !hasNextPage) {
      return;
    }

    _isLoadingMore = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response = await _fetchPage(_page + 1);

      _items.addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage = 'O servidor demorou mais que o esperado para carregar mais registros.';
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage = 'Não foi possível interpretar o histórico de treinos.';
    } catch (_) {
      _errorMessage = 'Não foi possível carregar mais registros.';
    } finally {
      _isLoadingMore = false;
      notifyListeners();
    }
  }

  Future<void> nextPage() async {
    if (!hasNextPage || _isLoading) {
      return;
    }

    await _loadPage(_page + 1);
  }

  Future<void> previousPage() async {
    if (!hasPreviousPage || _isLoading) {
      return;
    }

    await _loadPage(_page - 1);
  }

  Future<void> _loadPage(int targetPage) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response = await _fetchPage(targetPage);

      _replaceItems(response);
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage = 'O servidor demorou mais que o esperado.';
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage = 'Não foi possível interpretar o histórico de treinos.';
    } catch (_) {
      _errorMessage = 'Não foi possível carregar esta página.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<PagedWorkoutHistoryResponse> _fetchPage(int page) {
    final studentId = _studentId;

    if (studentId == null) {
      return _service.getMyHistory(page: page, pageSize: 20);
    }

    return _service.getStudentHistory(
      studentId: studentId,
      page: page,
      pageSize: 20,
    );
  }

  void _replaceItems(PagedWorkoutHistoryResponse response) {
    _items
      ..clear()
      ..addAll(response.items);

    _page = response.page;
    _totalPages = response.totalPages;
    _totalCount = response.totalCount;
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 400) {
      return 'Não foi possível carregar o histórico de treinos.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para visualizar este histórico.';
    }

    if (exception.statusCode == 404) {
      return 'Histórico não encontrado.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de histórico está temporariamente indisponível.';
    }

    return 'Não foi possível carregar o histórico de treinos.';
  }
}
