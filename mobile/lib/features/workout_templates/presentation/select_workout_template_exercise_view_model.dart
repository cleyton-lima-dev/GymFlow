import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:http/http.dart' as http;

class SelectWorkoutTemplateExerciseViewModel
    extends ChangeNotifier {
  SelectWorkoutTemplateExerciseViewModel(
      this._service,
      );

  final ExercisesService _service;

  final List<ExerciseSummary> _items = [];

  Timer? _searchDebounce;

  String _search = '';

  int _page = 1;
  int _totalPages = 1;

  bool _isLoading = false;
  bool _isLoadingMore = false;

  String? _errorMessage;

  List<ExerciseSummary> get items =>
      List.unmodifiable(_items);

  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;

  String? get errorMessage => _errorMessage;

  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response = await _service.getExercises(
        search: _search,
        isActive: true,
        page: 1,
        pageSize: 20,
      );

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A busca demorou mais que o esperado.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os exercícios.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar os exercícios.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void setSearch(String value) {
    _search = value.trim();

    _searchDebounce?.cancel();

    _searchDebounce = Timer(
      const Duration(milliseconds: 400),
      loadInitial,
    );
  }

  Future<void> refresh() async {
    await loadInitial();
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
      final nextPage = _page + 1;

      final response = await _service.getExercises(
        search: _search,
        isActive: true,
        page: nextPage,
        pageSize: 20,
      );

      _items.addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A busca demorou mais que o esperado.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os exercícios.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar mais exercícios.';
    } finally {
      _isLoadingMore = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
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
    _searchDebounce?.cancel();
    super.dispose();
  }
}
