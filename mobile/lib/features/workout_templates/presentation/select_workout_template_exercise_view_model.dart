import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:http/http.dart' as http;

class SelectWorkoutTemplateExerciseViewModel extends ChangeNotifier {
  SelectWorkoutTemplateExerciseViewModel(this._service);

  final ExercisesService _service;

  final List<ExerciseSummary> _items = [];

  Timer? _searchDebounce;

  int _requestVersion = 0;
  bool _isDisposed = false;

  String _search = '';

  int _page = 1;
  int _totalPages = 1;

  bool _isLoading = false;
  bool _isLoadingMore = false;

  String? _errorMessage;

  List<ExerciseSummary> get items => List.unmodifiable(_items);

  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;

  String? get errorMessage => _errorMessage;

  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() {
    return _loadInitial(++_requestVersion);
  }

  Future<void> _loadInitial(int requestVersion) async {
    if (_isStale(requestVersion)) {
      return;
    }

    final search = _search;

    _isLoading = true;
    _isLoadingMore = false;
    _errorMessage = null;
    _notifySafely();

    try {
      final response = await _service.getExercises(
        search: search,
        isActive: true,
        page: 1,
        pageSize: 20,
      );

      if (_isStale(requestVersion)) {
        return;
      }

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
    } on ApiException catch (exception) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'A busca demorou mais que o esperado.';
    } on http.ClientException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível interpretar os exercícios.';
    } catch (_) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível carregar os exercícios.';
    } finally {
      if (!_isStale(requestVersion)) {
        _isLoading = false;
        _isLoadingMore = false;
        notifyListeners();
      }
    }
  }

  void setSearch(String value) {
    final normalized = value.trim();

    if (_search == normalized) {
      return;
    }

    _search = normalized;

    _searchDebounce?.cancel();

    final requestVersion = ++_requestVersion;

    _searchDebounce = Timer(const Duration(milliseconds: 400), () {
      _loadInitial(requestVersion);
    });
  }

  Future<void> refresh() async {
    await loadInitial();
  }

  Future<void> loadMore() async {
    if (_isLoading || _isLoadingMore || !hasNextPage) {
      return;
    }

    final requestVersion = _requestVersion;
    final search = _search;
    final nextPage = _page + 1;

    _isLoadingMore = true;
    _errorMessage = null;
    _notifySafely();

    try {
      final response = await _service.getExercises(
        search: search,
        isActive: true,
        page: nextPage,
        pageSize: 20,
      );

      if (_isStale(requestVersion)) {
        return;
      }

      _items.addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
    } on ApiException catch (exception) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'A busca demorou mais que o esperado.';
    } on http.ClientException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível interpretar os exercícios.';
    } catch (_) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível carregar mais exercícios.';
    } finally {
      if (!_isStale(requestVersion)) {
        _isLoadingMore = false;
        notifyListeners();
      }
    }
  }

  bool _isStale(int requestVersion) {
    return _isDisposed || requestVersion != _requestVersion;
  }

  void _notifySafely() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar os exercícios.';
    }

    if (exception.statusCode >= 500) {
      return 'O banco de exercícios está temporariamente indisponível.';
    }

    return 'Não foi possível carregar os exercícios.';
  }

  @override
  void dispose() {
    _isDisposed = true;
    ++_requestVersion;

    _searchDebounce?.cancel();

    super.dispose();
  }
}
